# Spring Hinge Bash Toy

This sample adds Play Mode shot controls and diagnostic traces to a spring-hinge magnet toy in a real VPE table. It drives the local `Player`, `PhysicsEngine`, `SpringHingeApi`, and `MagnetApi`; it does not create or send hardware outputs.

1. Import **Spring Hinge Bash Toy** from Package Manager.
2. In a table scene, create the toy with **GameObject > Pinball > Spring Hinge Bash Toy**, then position its pivot and use the scene handles to fit the visual and analytic box.
3. Add `SpringHingeBashToyController` beneath the table, assign its Player, Spring Hinge, Owned Magnet, and a shot marker. Point the marker's forward axis toward the toy. Assigning a ball prefab is optional.
4. Enter Play Mode. Use **1**, **2**, and **3** for weak, medium, and strong shots; **M** to toggle the magnet; **T** to schedule a release; and **R** to release the ball, reset the hinge, and remove balls created by the sample. The optional on-screen panel exposes the same controls.

Start with the preset and move the magnet to the intended collision face. The held-ball-centre marker should sit one ball radius outside the analytic box. Raise magnetic strength and holding capacity together only when a strong shot should capture; a large influence radius does not make the attachment stiffer.

The diagnostic trace reports shot creation, hinge angle, impact, capture, release, and reset events. Use it with the Physics diagnostics in the editor to distinguish a magnetic release from the release-before-legacy-active-mechanism fallback.
