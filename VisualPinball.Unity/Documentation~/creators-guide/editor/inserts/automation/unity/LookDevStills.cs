// Scene setup for the inserts documentation's shots in the LookDev project (VisualPinball.Unity.Hdrp/LookDev~):
// the showcase on and off, the objects that get in its way, and the source insert of the pattern shots.
// Copy this file into LookDev~/Assets/Editor, use it through mcp.sh execute_menu_item, and delete it again.
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VisualPinball.Unity;

public static class LookDevStills
{
	private const string Showcase = "Insert Showcase";
	// Active in the scene, and over or among the showcase: a board over its middle inserts, a grid of unlit inserts.
	private static readonly string[] InTheWay = { "Playfield - Inserts", "Insert Grid, Off" };
	private const string PatternsSource = "Insert, Patterns";

	private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<Transform>()
		.Select(t => t.gameObject)
		.FirstOrDefault(g => g.name == name && g.scene.IsValid());

	/// <summary>Turns the showcase on, which the scene keeps inactive, and the objects in its way off.</summary>
	[MenuItem("Automation Stills/LookDev/Showcase On")]
	public static void ShowcaseOn()
	{
		Find(Showcase).SetActive(true);
		foreach (var name in InTheWay) {
			Find(name)?.SetActive(false);
		}
	}

	/// <summary>The scene as it was: the showcase off, the objects in its way on.</summary>
	[MenuItem("Automation Stills/LookDev/Showcase Off")]
	public static void ShowcaseOff()
	{
		Find(Showcase).SetActive(false);
		foreach (var name in InTheWay) {
			Find(name)?.SetActive(true);
		}
	}

	/// <summary>
	/// The inactive insert Pinball/Automation/Patterns/Shoot takes its copies from: a 47 mm square with
	/// a channel, sized for every pattern to show.
	/// </summary>
	[MenuItem("Automation Stills/LookDev/Create Patterns Source")]
	public static void CreatePatternsSource()
	{
		DeletePatternsSource();
		var go = new GameObject(PatternsSource);
		go.SetActive(false);
		var insert = go.AddComponent<InsertComponent>();
		var so = new SerializedObject(insert);
		so.FindProperty("_shape").enumValueIndex = (int)InsertShape.Rectangle;
		foreach (var (name, value) in new[] { ("_width", 47f), ("_height", 47f), ("_depth", 4.68f), ("_channelWidth", 2.83f), ("_channelDepth", 7.11f), ("_starburstOffset", 1f), ("_ribWidth", 6f), ("_ringRoundness", 0f), ("_stippleHeight", -1f) }) {
			so.FindProperty(name).floatValue = value;
		}
		so.FindProperty("_starburstBursts").intValue = 24;
		so.FindProperty("_holeSegments").intValue = 8;
		so.ApplyModifiedPropertiesWithoutUndo();
	}

	[MenuItem("Automation Stills/LookDev/Delete Patterns Source")]
	public static void DeletePatternsSource()
	{
		foreach (var insert in Object.FindObjectsByType<InsertComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(i => i.name == PatternsSource)) {
			Object.DestroyImmediate(insert.gameObject);
		}
	}
}
