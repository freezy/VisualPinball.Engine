# Motion Controller guide: screenshots and visual brief

This is a production checklist for [the motion controller guide](../creators-guide/manual/mechanisms/motion-controllers.md). It is outside the DocFX content directories and is not a published manual page. The guide contains two working Mermaid diagrams and commented image slots; uncomment each image only after its file has been supplied and checked.

## Example and scope

Use the Terminator 2 gun for rotation and pose preview. Assume the visual already has its origin on the rotation axis. The current Motion Controller cannot replace its repeating run/stop motor behavior. Capture a separate demonstration copy of the gun visuals, with existing animation drivers removed from that copy. Keep the working T2 mechanism intact. Screenshots must not imply that mapping the T2 motor coil to Follow Coil recreates the motor.

The main gun in the local T2 scene currently uses a PinMAME Mech Handler with 240 steps and an event rotation factor of -1/3 around local Y. This motivates the tutorial's illustrative -80-degree rotation offset; it is not a measured hardware specification. Confirm the axis and orientation on the copied model.

Use only T2 or neutral component examples. Exclude imagery, object names, and mechanism examples from other licensed tables, including incidental background content in Unity.

## Shared capture setup

Create the demonstration hierarchy below a table Player:

```text
Gun Travel                         Motion Controller
├── Fixed Mount                    Stationary visual geometry
└── Gun Visuals                    Motion Transform and moving geometry
```

Attach Motion Transform directly to Gun Visuals. Its authored origin is on the physical rotation shaft. The guide's infobox explains the alternative of an empty parent pivot when the visual's origin is unsuitable; the main screenshots use the direct setup. Use a clear neutral background and a camera that shows the shaft, gun barrel, and fixed mounting hardware. Keep the same hierarchy names and settings throughout the screenshots. Preserve readable Inspector text; crop unrelated Unity panels. PNG is preferred for screenshots, SVG for the plotted diagram.

Save final media next to `motion-controllers.md` in `creators-guide/manual/mechanisms/`. The filenames below match the image slots already in the guide.

## 1. Visual origin and hierarchy

**File:** `motion-controller-t2-pivot.png`

Show the Hierarchy and Scene view together, with Gun Visuals selected. Expand Gun Travel to show Fixed Mount and Gun Visuals. Do not insert a separate pivot in the main screenshot. Enable the rotation gizmo in Local mode and place it visibly at the shaft. Add short callouts for “Rotation axis”, “Moving gun”, and “Fixed mount”. The reader should immediately see which geometry rotates and which remains stationary.

**Caption/alt text:** Gun hierarchy and Scene view, with Motion Transform directly on Gun Visuals, its origin on the rotation shaft, and fixed mounting hardware outside the moving branch.

## 2. Transform Inspector

**File:** `motion-controller-t2-transform-inspector.png`

Capture the Motion Transform component on Gun Visuals, using these values:

| Field | Value |
| --- | --- |
| Emitter | Gun Travel's Motion Controller |
| Animate Position | Off |
| Animate Rotation | On |
| Rotation Offset | X 0, Y -80, Z 0 |
| Input Min | 0 |
| Input Max | 1 |
| Response Curve | Linear from (0, 0) to (1, 1) |
| Reverse | Off |

Show the component title, fields, and kinematic help text. Use the exact Inspector labels rather than replacing them with artwork. If the chosen model requires another axis or sweep, record the final values so the guide and this brief can be updated together.

**Caption/alt text:** Motion Transform inspector on Gun Visuals, showing rotation enabled, a local Y rotation offset, and a linear response over the full input range.

## 3. Pose comparison

**File:** `motion-controller-t2-preview.png`

Produce three panels of the same gun with the same camera, crop, and scale, labeled “Position 0”, “Position 0.5”, and “Position 1”. Keep the shaft location marked in each panel. With the settings above, the rotations relative to the authored pose are 0, -40, and -80 degrees. Leave Reverse off.

Select Gun Travel and use its Preview Position slider. A value of zero restores the authored pose. Show enough fixed mounting geometry to demonstrate that it stays still. This is a pose comparison, so do not imply that the three samples are equally spaced in time under an ease curve.

**Caption/alt text:** Three views of the gun at preview positions 0, 0.5, and 1, with the same camera and the rotation axis marked.

## 4. Motion Controller Inspector

**File:** `motion-controller-coil-inspector.png`

Capture the Motion Controller component with these default settings, showing its Animation Preview controls as well:

| Field | Value |
| --- | --- |
| Coil Mode | Follow Coil |
| Initial Position | 0 |
| Activation Duration | 0.3 s |
| Activation Curve | Ease-in/ease-out from (0, 0) to (1, 1) |
| Release Duration | 0.3 s |
| Release Curve | Ease-in/ease-out from (0, 0) to (1, 1) |
| Activation Threshold | 0.001 |
| Release Delay | 0.05 s |
| Position Switches | Empty |
| Preview Position | 0.5 |

This image illustrates the generic Motion Controller controls. Do not include a T2 Gun Motor coil mapping in the frame.

**Caption/alt text:** Motion Controller inspector showing Follow Coil, startup position, travel durations and curves, activation threshold, and release delay.

## 5. Input interval and Reverse

**File:** `motion-controller-response-window.svg`

Draw a clean two-dimensional plot. Horizontal axis: “Motion Controller position”, 0–1. Vertical axis: “Follower response”, 0–1. Mark 0, 0.25, 0.5, 0.75, and 1 on the horizontal axis. Shade the interval 0.25–0.75 and label its boundaries “Input Min” and “Input Max”.

Draw two traces with a legend:

- **Normal:** (0, 0) → (0.25, 0) → (0.75, 1) → (1, 1).
- **Reverse:** (0, 1) → (0.25, 1) → (0.75, 0) → (1, 0).

Use a different line style as well as color for the Reverse trace. Label the outer regions “Hold endpoint”. The graph assumes a linear Response Curve and shows position mapping, not time. This is a generic explanation of follower behavior, not the T2 motor's cam profile.

**Caption/alt text:** Follower response is zero up to motion controller position 0.25, rises linearly to one at 0.75, and stays at one for the remaining travel. A second curve shows Reverse using the same interval.

## 6. Position Switches Inspector

**File:** `motion-controller-position-switches.png`

Expand Position Switches and capture three entries:

| Name | Switch Type | Between | Pulse Every | Pulse Duration |
| --- | --- | --- | --- | --- |
| Home | Enable Between | 0–0.02 | Not shown | Not shown |
| End | Enable Between | 0.98–1 | Not shown | Not shown |
| Encoder | Always Pulse | Not shown | 0.1 | 20 ms |

Crop to keep all three entries and their labels readable. These ranges illustrate generic maintained and pulse switches. Do not label them Gun Home or Gun Mark, and do not show T2 ROM switch IDs.

**Caption/alt text:** Position Switches inspector with maintained Home and End ranges and an Always Pulse encoder configured for pulses every 0.1 travel.

## Page order

The published guide explains the two components first: Motion Controller, Motion Transform, preview, and collision. The T2 example follows those reference sections. The numbered captures in this brief are production tasks, not the order of sections on the page.

## Delivery check

- Keep exact filenames or update the matching image slots.
- Verify Inspector labels, values, axis direction, and pose labels against the guide.
- Check legibility at the documentation page's normal reading width.
- Uncomment the matching Markdown image in the guide once the asset exists.
- Rebuild DocFX and check the completed page in light and dark themes.
