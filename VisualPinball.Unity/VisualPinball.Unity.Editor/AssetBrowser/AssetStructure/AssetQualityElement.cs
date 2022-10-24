// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// The visual element for read-only presentation of a quality tag.
	/// for write-enabled presentation.
	/// </summary>
	public class AssetQualityElement : VisualElement
	{
		private readonly AssetQuality _quality;
		private AssetBrowser _browser;

		public AssetQualityElement(AssetQuality quality)
		{
			_quality = quality;

			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetStructure/AssetQualityElement.uss");
			styleSheets.Add(styleSheet);

			RegisterCallback<AttachToPanelEvent>(OnAttached);
		}

		private void OnAttached(AttachToPanelEvent evt)
		{
			_browser = panel.visualTree.userData as AssetBrowser;
			Bind();
		}

		private void Bind()
		{
			var qualityToggle = new ToolbarToggle {
				text = Name,
				value = _browser != null && _browser.Query.HasQuality(_quality)
			};
			qualityToggle.RegisterValueChangedCallback(evt => OnToggle(_quality, evt.newValue));

			var qualityDescription = new Label {
				text = Description
			};

			Add(qualityToggle);
			Add(qualityDescription);
		}

		private string Name => _quality switch {
			AssetQuality.Imprecise => "Imprecise",
			AssetQuality.BasedOnPhotos => "Based on Photos",
			AssetQuality.Measured => "Measured",
			AssetQuality.Scanned => "Scanned",
			AssetQuality.FromCadModel => "From CAD Model",
			_ => throw new ArgumentOutOfRangeException()
		};

		private string Description => _quality switch {
			AssetQuality.Imprecise => "It's unknown how this asset was created, and it needs to be checked against real-world measures.",
			AssetQuality.BasedOnPhotos => "This asset was modeled based on photo references, so the measures might be off.",
			AssetQuality.Measured => "This asset was modeled based on real-world measures and should be of good quality.",
			AssetQuality.Scanned => "This asset was 3D-scanned and should be of excellent quality.",
			AssetQuality.FromCadModel => "This asset was modeled using a CAD model from the manufacturer. This is the highest possible quality.",
			_ => throw new ArgumentOutOfRangeException()
		};

		private void OnToggle(AssetQuality quality, bool isToggled)
		{
			if (!isToggled) {
				_browser.FilterByQuality(quality, true);

			} else {
				_browser.FilterByQuality(quality);
			}
		}
	}
}
