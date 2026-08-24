// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[InitializeOnLoad]
	internal static class RubberGuideDependencyTracker
	{
		private static readonly Dictionary<EntityId, HashSet<RubberComponent>> Dependents = new();
		private static readonly Dictionary<RubberComponent, FailedBake> FailedBakes = new();
		private static readonly HashSet<RubberComponent> Pending = new();
		private static bool _indexDirty = true;
		private static bool _isRebaking;
		private static double _rebakeAt;

		private readonly struct FailedBake
		{
			public readonly Hash128 AuthoringHash;
			public readonly string Error;

			public FailedBake(Hash128 authoringHash, string error)
			{
				AuthoringHash = authoringHash;
				Error = error;
			}
		}

		static RubberGuideDependencyTracker()
		{
			ObjectChangeEvents.changesPublished += ChangesPublished;
			EditorApplication.update += Update;
			AssemblyReloadEvents.beforeAssemblyReload += Unregister;
			PrefabStage.prefabStageOpened += PrefabStageChanged;
			PrefabStage.prefabStageClosing += PrefabStageChanged;
		}

		internal static void RebuildSoon()
		{
			_indexDirty = true;
		}

		internal static void GuidesChanged(IEnumerable<RubberGuideComponent> guides)
		{
			if (guides == null) {
				return;
			}
			RebuildIndex();
			foreach (var guide in guides) {
				if (guide) {
					QueueDependents(guide.GetEntityId());
				}
			}
			SceneView.RepaintAll();
		}

		/// <summary>Queues guided rubbers whose own authoring inputs changed.</summary>
		internal static void RubbersChanged(IEnumerable<RubberComponent> rubbers)
		{
			if (rubbers == null) {
				return;
			}
			foreach (var rubber in rubbers) {
				QueueRubber(rubber);
			}
			SceneView.RepaintAll();
		}

		private static void Unregister()
		{
			ObjectChangeEvents.changesPublished -= ChangesPublished;
			EditorApplication.update -= Update;
			AssemblyReloadEvents.beforeAssemblyReload -= Unregister;
			PrefabStage.prefabStageOpened -= PrefabStageChanged;
			PrefabStage.prefabStageClosing -= PrefabStageChanged;
		}

		private static void PrefabStageChanged(PrefabStage _)
		{
			_indexDirty = true;
			Pending.Clear();
			FailedBakes.Clear();
		}

		private static void ChangesPublished(ref ObjectChangeEventStream stream)
		{
			if (_isRebaking) {
				return;
			}
			for (var i = 0; i < stream.length; i++) {
				var kind = stream.GetEventType(i);
				if (kind == ObjectChangeKind.ChangeGameObjectOrComponentProperties) {
					stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var args);
					var changed = EditorUtility.EntityIdToObject(args.entityId);
					QueueDependents(args.entityId);
					if (changed is RubberComponent rubber) {
						QueueRubber(rubber);
						_indexDirty = true;
					} else if (changed is GameObject gameObject
						&& gameObject.TryGetComponent<RubberComponent>(out var gameObjectRubber)) {
						QueueRubber(gameObjectRubber);
						_indexDirty = true;
					}
				} else if (kind == ObjectChangeKind.ChangeGameObjectParent
					|| kind == ObjectChangeKind.ChangeGameObjectStructure
					|| kind == ObjectChangeKind.ChangeGameObjectStructureHierarchy
					|| kind == ObjectChangeKind.ChangeScene
					|| kind == ObjectChangeKind.CreateGameObjectHierarchy
					|| kind == ObjectChangeKind.DestroyGameObjectHierarchy
					|| kind == ObjectChangeKind.UpdatePrefabInstances) {
					_indexDirty = true;
				}
			}
		}

		private static void QueueDependents(EntityId entityId)
		{
			if (!Dependents.TryGetValue(entityId, out var rubbers)) {
				return;
			}
			foreach (var rubber in rubbers) {
				QueueRubber(rubber);
			}
		}

		private static void QueueRubber(RubberComponent rubber)
		{
			if (rubber && rubber.PathSource == RubberPathSource.Guides) {
				var authoringHash = RubberGuideResolver.ComputeAuthoringHash(rubber);
				if (HasUnchangedFailure(rubber, authoringHash)) {
					return;
				}
				FailedBakes.Remove(rubber);
				var wasEmpty = Pending.Count == 0;
				Pending.Add(rubber);
				ScheduleNextUpdate(wasEmpty);
			}
		}

		private static bool HasUnchangedFailure(RubberComponent rubber,
			Hash128 authoringHash)
		{
			return FailedBakes.TryGetValue(rubber, out var failure)
				&& failure.AuthoringHash == authoringHash;
		}

		private static void ScheduleNextUpdate(bool wasEmpty)
		{
			// Keep one pending bake per editor update. Repeated changes replace its input
			// without postponing it, so transform and inspector drags stay visually live.
			if (wasEmpty && Pending.Count > 0) {
				_rebakeAt = EditorApplication.timeSinceStartup;
			}
		}

		private static void Update()
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode) {
				return;
			}
			if (_indexDirty) {
				RebuildIndex();
			}
			if (Pending.Count == 0 || EditorApplication.timeSinceStartup < _rebakeAt) {
				return;
			}

			var pending = new List<RubberComponent>(Pending);
			Pending.Clear();
			_isRebaking = true;
			try {
				foreach (var rubber in pending) {
					if (!rubber || rubber.PathSource != RubberPathSource.Guides) {
						continue;
					}
					if (RubberAutofit.GetStatus(rubber).IsValid) {
						FailedBakes.Remove(rubber);
						continue;
					}
					// The bake is derived data. Recording a second undo operation makes one
					// guide edit require two undos and causes undo-triggered rebake loops.
					if (RubberGuideCommands.TryBakeAligned(rubber,
						"Update Guided Rubber", true, out _, out var error)) {
						FailedBakes.Remove(rubber);
						EditorUtility.SetDirty(rubber);
						PrefabUtility.RecordPrefabInstancePropertyModifications(rubber);
					} else {
						var authoringHash = RubberGuideResolver.ComputeAuthoringHash(rubber);
						var shouldLog = !FailedBakes.TryGetValue(rubber, out var previousFailure)
							|| previousFailure.AuthoringHash != authoringHash
							|| previousFailure.Error != error;
						FailedBakes[rubber] = new FailedBake(authoringHash, error);
						if (shouldLog) {
							Debug.LogWarning($"Could not update guided rubber '{rubber.name}': {error}", rubber);
						}
					}
				}
			} finally {
				_isRebaking = false;
			}
			SceneView.RepaintAll();
		}

		private static void RebuildIndex()
		{
			Dependents.Clear();
			foreach (var destroyed in FailedBakes.Keys.Where(rubber => !rubber).ToArray()) {
				FailedBakes.Remove(destroyed);
			}
			foreach (var rubber in Object.FindObjectsByType<RubberComponent>(FindObjectsInactive.Include)) {
				if (!rubber || EditorUtility.IsPersistent(rubber)
					|| rubber.PathSource != RubberPathSource.Guides) {
					continue;
				}
				foreach (var binding in rubber.GuideBindings) {
					if (!binding.Guide) {
						continue;
					}
					AddDependency(binding.Guide.GetEntityId(), rubber);
					AddTransformHierarchyDependencies(binding.Guide.transform, rubber);
				}
				AddTransformHierarchyDependencies(rubber.transform, rubber);
				var authoringHash = RubberGuideResolver.ComputeAuthoringHash(rubber);
				if (!HasUnchangedFailure(rubber, authoringHash)
					&& !RubberAutofit.GetStatus(rubber).IsValid) {
					QueueRubber(rubber);
				}
			}
			_indexDirty = false;
		}

		private static void AddTransformHierarchyDependencies(Transform transform,
			RubberComponent rubber)
		{
			for (var current = transform; current; current = current.parent) {
				AddDependency(current.GetEntityId(), rubber);
				AddDependency(current.gameObject.GetEntityId(), rubber);
			}
		}

		private static void AddDependency(EntityId entityId, RubberComponent rubber)
		{
			if (!Dependents.TryGetValue(entityId, out var rubbers)) {
				rubbers = new HashSet<RubberComponent>();
				Dependents.Add(entityId, rubbers);
			}
			rubbers.Add(rubber);
		}
	}
}
