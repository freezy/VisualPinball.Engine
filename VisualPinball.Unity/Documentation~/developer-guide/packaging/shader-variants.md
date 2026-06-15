---
uid: developer-guide-packaging-shader-variants
title: Packaging Shader Variants
description: Why runtime-resolved materials need a shipped shader variant collection in player builds, and how to keep it current as tables are added.
---

# Shader Variants

This page is for people building and shipping a player. It explains why a table that looks correct in the editor can render with broken materials in a standalone build, and what you do about it when you add a new table.

It is specifically about HDRP. Other renderers that resolve materials at runtime will hit the same class of problem, but the keyword and tooling details below are HDRP/Unity-specific.

## Why a build needs help

The HDRP resolver builds its materials at runtime: it clones a small set of template materials and flips HDRP/Lit keywords (surface type, refraction model, transmission, SSR-transparent, thickness, double-sided, and so on) to match each `materials.json` profile. See [import](import.md) for the resolver flow.

Those keywords are HDRP `shader_feature` keywords, and `shader_feature` behaves differently in the editor than in a build:

- In the **editor** the shader compiler is present. The first time a material asks for a keyword combination that has not been compiled yet, the editor compiles it on demand and caches it (the *"Compiling shader variants…"* hitches). A runtime-built material always finds its variant, so the table looks correct.
- In a **player build** there is no shader compiler. Only the `shader_feature` combinations that some material asset uses at build time — or that a `ShaderVariantCollection` referenced from *Project Settings → Graphics → Preloaded Shaders* lists — are compiled and shipped. The resolver's runtime combinations are not present on any build-time material, so without a preloaded collection they are stripped. HDRP then substitutes the nearest available variant, and the visible result is broken transparency, refraction, and reflections — even though import itself succeeds.

This is why the editor and a build of the *same* table off the *same* `.vpe` can look different. It is not a packaging problem; the `.vpe` is renderer-agnostic and correct. The variants are a property of the **player + render pipeline + platform + engine version**, not of the table, which is why they live in the player and not in the package.

## The fix: a preloaded ShaderVariantCollection

A player ships a `ShaderVariantCollection` that covers the variants the resolver produces, referenced from *Project Settings → Graphics → Preloaded Shaders*. The build then keeps exactly those variants (and only those), so runtime-built materials find the right one.

Because the needed combinations depend on per-material data (which texture maps and material features each profile enables) and on per-pass keyword differences, the collection cannot be reliably enumerated by hand. It is **captured** from real table loads instead, which records exactly the `(shader, pass, keyword)` tuples that are actually drawn.

## Adding variants for a new table

A new table can introduce material/keyword combinations the collection has not seen yet. When you add one, capture it and rebuild:

1. **Clear the tracked set.** *Project Settings → Graphics*, in the shader-loading section, clear the currently-tracked shader variants.
2. **Exercise the table.** Enter play mode, load the table, and move the camera so every insert, plastic, ramp, and post is actually drawn — only drawn variants are recorded. To broaden coverage, load several tables in the same session **without clearing in between**; the tracked set accumulates.
3. **Save to asset.** Save the tracked set to the player's `.shadervariants` collection.
4. **Preload it.** Make sure the collection is referenced under *Preloaded Shaders* (the player provides a menu helper that wires it for you).
5. **Rebuild** and verify the table renders correctly in the build.

Capture while the editor is on the **same quality level the build ships** (see the quality-level note below), so the recorded variants match the shipped pipeline.

## Things that bite

- **Quality level / pipeline must match.** Variants captured under one HDRP pipeline asset may not be exactly what another needs. Develop and capture on the quality level you actually ship.
- **Ray tracing is kept out of player builds for now.** The DXR pipeline pulls in ray-tracing passes that the imported glTF shadergraphs cannot compile, and it multiplies the variant count enormously. The shipped quality level is a rasterized one (deferred + SSR); the DXR asset stays in the project for future work but is not the player default.
- **The imported glTF shadergraphs are intentionally not in the build.** The HDRP glTF material generator builds its import placeholders on HDRP/Lit, and the resolver replaces every imported material by name, so the original glTF shadergraph is never actually used and does not need to ship. Keeping it out also sidesteps a real problem: gltFast's glTF materials are HDRP Shader Graphs, so HDRP auto-generates ray-tracing passes for them (`ForwardDXR`, `GBufferDXR`, …), and gltFast's versions have an invalid gradient `Sample` in the ray-tracing `closesthit` context. Those passes are only compiled when ray tracing is enabled in the build, so they failed only while the standalone still defaulted to the DXR pipeline — and they would break a future ray-tracing build the same way if the shadergraphs were shipped. A harmless "shader missing" line for those shadergraphs may appear in the *Development Console*; it does not affect rendering and does not appear in non-development builds.
- **Guard against regressions.** The player fails a standalone build when the collection is missing, empty, or not preloaded, so a fresh checkout or a settings reset cannot silently ship broken transparency.
