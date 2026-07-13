// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[InitializeOnLoad]
	internal static class RubberGuideDependencyTracker
	{
		private const double DebounceSeconds = 0.15;
		private static readonly Dictionary<EntityId, HashSet<RubberComponent>> Dependents = new();
		private static readonly Dictionary<EntityId, double> IgnoredRubberChanges = new();
		private static readonly HashSet<RubberComponent> Pending = new();
		private static bool _indexDirty = true;
		private static bool _isRebaking;
		private static double _rebakeAt;

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
					QueueDependents(args.entityId);
					var changed = EditorUtility.EntityIdToObject(args.entityId);
					if (IsIgnoredRubberChange(changed)) {
						continue;
					}
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
				if (rubber) {
					Pending.Add(rubber);
				}
			}
			_rebakeAt = EditorApplication.timeSinceStartup + DebounceSeconds;
		}

		private static void QueueRubber(RubberComponent rubber)
		{
			if (rubber && rubber.PathSource == RubberPathSource.Guides) {
				Pending.Add(rubber);
				_rebakeAt = EditorApplication.timeSinceStartup + DebounceSeconds;
			}
		}

		private static void Update()
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode) {
				return;
			}
			RemoveExpiredIgnoredChanges();
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
						continue;
					}
					// The bake is derived data. Recording a second undo operation makes one
					// guide edit require two undos and causes undo-triggered rebake loops.
					IgnoreDerivedChanges(rubber);
					if (RubberAutofit.TryBake(rubber, out _, out var error)) {
						EditorUtility.SetDirty(rubber);
						PrefabUtility.RecordPrefabInstancePropertyModifications(rubber);
					} else {
						Debug.LogWarning($"Could not update guided rubber '{rubber.name}': {error}", rubber);
					}
				}
			} finally {
				_isRebaking = false;
			}
			SceneView.RepaintAll();
		}

		private static void IgnoreDerivedChanges(RubberComponent rubber)
		{
			var until = EditorApplication.timeSinceStartup + DebounceSeconds * 2.0;
			IgnoredRubberChanges[rubber.GetEntityId()] = until;
			IgnoredRubberChanges[rubber.gameObject.GetEntityId()] = until;
		}

		private static bool IsIgnoredRubberChange(Object changed)
		{
			if (!changed) {
				return false;
			}
			var rubber = changed as RubberComponent;
			if (!rubber && changed is GameObject gameObject) {
				gameObject.TryGetComponent(out rubber);
			}
			return rubber && IgnoredRubberChanges.TryGetValue(rubber.GetEntityId(), out var until)
				&& EditorApplication.timeSinceStartup <= until;
		}

		private static void RemoveExpiredIgnoredChanges()
		{
			var now = EditorApplication.timeSinceStartup;
			var expired = new List<EntityId>();
			foreach (var pair in IgnoredRubberChanges) {
				if (pair.Value < now) {
					expired.Add(pair.Key);
				}
			}
			foreach (var entityId in expired) {
				IgnoredRubberChanges.Remove(entityId);
			}
		}

		private static void RebuildIndex()
		{
			Dependents.Clear();
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
