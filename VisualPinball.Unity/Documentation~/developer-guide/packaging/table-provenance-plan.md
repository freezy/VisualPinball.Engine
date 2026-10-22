---
uid: developer-guide-table-provenance-plan
title: Table provenance and shared asset reuse implementation plan
description: Preserve versioned table credits and metadata hashes, share cooked textures by content, and reuse matching asset-library sounds during import.
---

# Table provenance and shared asset reuse implementation plan

Full table imports retain source UUID, version, and credits, including known ancestral credits, even after substantial editing. Every re-export carries those records. Each table's root hash commits to its metadata and immediate sources; deeper parent hierarchies can be recovered from the corresponding source packages. Separately, a global SHA-256 texture cache reuses unchanged cooked textures across updates and tables, and editor imports reuse matching asset-library sounds instead of extracting duplicate audio files.

Implemented on 22 October 2026 on `feat/checksum`. Fable 5.1 reviewed the design. Opus 5.5 reviewed the implementation in the resumed Claude session; a final independent Fable 5.1 review used a fresh session. Both final reviews found no blockers. The final Unity regression run passed 57 tests with one unsupported-symlink skip, and the standalone integrity suite passed 22 tests. Core, runtime, editor, and player compilation passed. The following design records the intended behavior and qualification cases. The implementation notes below take precedence where an optional mechanism or proposed benchmark has not shipped. Review transcripts and test output are retained with the task artifacts.

## Implementation notes

- `TableProvenance` provides the canonical hashes and compact credit records. `TableComponent` stores a private serialized JSON snapshot and returns defensive copies. JSON fields use the C# property names (`SchemaVersion`, `TableId`, `RootHash`, etc.); `TableMetadata.Version` is serialized as `version`. The inspector caches its calculated snapshot until the component becomes dirty.
- Full editor imports derive; runtime loads preserve identity. The inspector offers explicit derivative creation, patch-version bumping, file comparison, and verification against a parent package selected by the user. Automatic parent discovery, recursive graph reconstruction, and remote retrieval are future extensions. No parent download is needed for normal use.
- Container format 2 requires provenance and `integrity.json`. ZIP admission streams and verifies all entries once; unchanged file length, modification/creation timestamps, manifest digest, and directory metadata permit persisted receipts to skip bulk hashing. This is a local change detector, not publisher authentication or protection against deliberate timestamp/receipt forgery. Limits are 16 MiB for the integrity manifest, 65,536 entries, 16 GiB per entry, and 64 GiB total uncompressed content.
- The shared texture cache uses a hash of source identity plus a versioned JSON recipe. Cache keys are local implementation details, separate from the portable canonical provenance encoding. The object contains a checksummed header and separately SHA-256-checked uncompressed payload; atomic publication and read-back verification precede its receipt. Normal/color roles split when needed. Unsupported compute paths cache encoded sources, which still need decoding on warm loads. Raw cooked hits avoid decoding/cooking and bulk hashes, but still read/decompress/upload texture bytes.
- Cache data lives beneath the per-user local application-data directory, `VisualPinball/VPE/TextureCache/v2`. Editor and player share that root; `VpeIntegrity.CacheRoot` can override it. The default soft quota is 10 GiB. Player settings expose 1/5/10/25/50 GiB. Trimming runs after creation or explicit maintenance, with in-process recency and file-write-time fallback after restart. Open file sharing protects active readers on Windows; atomic files remain usable through unlink on platforms that allow it. This release has no persistent cross-process LRU database or lease service. Stale temporary files are removed after a day during trimming. Old per-table cache files are no longer read and can be removed manually.
- The loader reads ahead by one object and yields after an upload time slice rather than once per hit. Individual objects and source texture reads are bounded at 512 MiB. Memory also includes Unity decode/cook work and resident GPU textures; that limit is not a total-process memory cap.
- Editor maintenance commands explicitly verify packages/cache, clear the shared texture cache, and rebuild the audio index. Silent payload corruption with unchanged timestamps/header can escape warm checks; explicit verification detects it. There is no background idle scrubber in this release. A failed forced check invalidates the receipt; a damaged texture object is rebuilt on its next use from the verified source package.
- The audio index discovers Unity audio assets in project and installed packages, resolves physical package paths, and incrementally persists exact/PCM hashes under `Library/VPE`. Initial discovery uses Unity's main thread and hashing runs on a worker. Incoming audio and chosen candidates are hashed again at matching time; those smaller per-sound checks remain synchronous. Complete supported importer intent is required. Matching never changes a reused asset's importer. Unsupported VPX WAV canonical matching takes the existing import path; Unity decoder failures are reported and processing continues, matching the prior converter behavior.
- Qualification includes independent canonical-hash fixtures, package/object receipts and corruption cases, concurrent publication, real Unity audio reuse, a GPU texture warm-hit test, and full export/runtime/editor/re-export identity preservation. Representative whole-table startup, restart-to-playable timing, and large-table memory profiling remain measurements to perform before claiming near-instant loading. Avoiding repeated SHA-256 does not remove scene construction or GPU upload costs.

## Outcomes and boundaries

1. Each authored table has a persistent UUID and an editable release version. Each released update bumps its version. Full editor import creates a new UUID and release sequence, preserving source identity/version and primary/secondary credits in read-only records.
2. Packages contain current metadata, direct source references, and a flat compact index of known ancestral UUIDs, versions, and credits. They do not embed their parents' full metadata hierarchies or all historical edges.
3. A root hash commits to current identity/metadata, direct source hashes, and inherited credits. Parent hashes can be referenced without downloading or recursively hashing parents. Deeper resolution is optional and never required to import, export, or play.
4. Local texture caching is shared across tables and versions. Identical source bytes and compatible cooking settings share one cooked object; changed textures cause individual misses.
5. VPE and VPX editor imports hash incoming audio, find compatible library clips, and bind them directly. Original table sound names, mappings, and playback settings survive even when library names differ.
6. Portable packages retain their source textures and sound bytes. This deduplicates local caches and imported assets, not source bytes inside separately downloaded archives. Mandatory hashes support first-use verification and repair; unchanged warm loads reuse persisted verification receipts and perform only cheap file/header checks, without bulk source or cooked-payload hashing. Remote asset delivery, perceptual matching, signatures, and automatic attribution for arbitrary copied meshes/scripts are separate work.

## Starting point and planned integration

Paths are relative to `VisualPinball.Engine/VisualPinball.Unity/` unless stated otherwise.

| Location | Observed behavior | Planned integration |
| --- | --- | --- |
| `VisualPinball.Unity/VPT/Table/TableMetadata.cs` | Editable title/authors; no UUID or release version. | Add current version; keep editable credits separate from captured records. |
| `VisualPinball.Unity/VPT/Table/TableComponent.cs` | Serialized Metadata and VPX TableInfo. | Private serialized provenance with immutable access. |
| `VisualPinball.Unity/Packaging/VpeNodeIds.cs` | Fresh transform UUIDs per export. | Do not reuse as table identity. |
| `VisualPinball.Unity.Editor/Packaging/PackageReader.cs` | Setup creates a directory; materials-only import skips table metadata; no completed-root Undo/failure cleanup. | Preflight before destination creation; capture provenance; commit or clean owned outputs. |
| `VisualPinball.Unity.Editor/Packaging/PackageWriter.cs` | Writes table/table.json; deletes destination before writing. | Capture metadata once, write provenance and asset digests, publish temporary output after success. |
| `VisualPinball.Unity/Packaging/RuntimePackageReader.cs` | Whole-table material cache lookup and cooking; item/ref prefetch. | Restore identity without deriving; resolve/cache/cook textures individually. |
| `VisualPinball.Unity/Packaging/VpeTableMetadataReader.cs` | Direct ZIP summary reads, file-information cache. | Bound the stream; use a separate provenance reader for comparisons. |
| `VisualPinball.Unity.Editor/Toolbox/ToolboxEditor.cs` | New Table uses the VPX converter with an empty container. | Explicit native initialization. |
| `VisualPinball.Unity.Editor/Import/VpxSceneConverter.cs` | Reuses table-name folders; ExtractSounds checks destination names, writes GetFileData(), then reloads destination paths. | Fresh full-import folder and explicit incoming-sound-to-clip resolution map. |
| `VisualPinball.Unity/Packaging/VpeTextureCache.cs` | One .vptc blob per package path/settings. GetCachePath hashes the absolute path; ComputeTextureIdentity hashes ZIP names, lengths, and CRC32 values. | Per-content derived objects. The existing SHA-256 over directory information is not a source-byte digest. |
| `VisualPinball.Unity/Packaging/VpeTextureCook.cs` | Plans/cooks a complete source blob, producing one blob. | Deterministic per-use recipes and selected misses. |
| `VisualPinball.Unity.Editor/Packaging/VpeTextureBlobSource.cs` | Loads final export bytes, including PNG 16-to-8-bit conversion. | Hash after conversion, over the actual packaged bytes. |
| `VisualPinball.Unity/Packaging/VpeTextureSources.cs` and `VpeMaterialReader.cs` | Transient source blob and per-ID upload descriptors. | Per-texture data handles, retaining current-package binding descriptors. |
| `VisualPinball.Unity/Packaging/PackagedFiles.cs` | VPE sound import trusts GUID matches, otherwise extracts. | Verify content/settings, resolve by audio index, extract only misses. |
| `VisualPinball.Unity/Sound/SoundPackable.cs` and `Packaging/VpeSoundImportPreprocessor.cs` | Capture/apply some AudioImporter settings. | Complete import-intent contract; never apply settings to reused library assets. |
| `VisualPinball.Unity/VPT/Table/LegacyContainer.cs` | LegacySound.Name/ToSound use AudioClip.name; WAV conversion assumes a 44-byte header. | Separate original names; parse real WAV chunks for rematched library files. |

## A. Compact versioned provenance

### Identity and version lifecycle

Add `TableMetadata.Version`, serialized as `version`. Native/newly derived tables start at `1.0.0`; provide a Bump patch action and manual editing. Newly authored versions use three nonnegative decimal components without leading zeros. Historical VPX labels remain verbatim and explicitly legacy; unknown historical versions are empty. A derivative starts its own sequence, while its source record keeps the original version.

Bump versions for every released update, including asset-only/gameplay changes. Do not silently increment on saves, refreshes, retries, or repeated development exports. Store a local last-exported `(UUID, version, rootHash)` receipt outside hashed metadata and warn/offer a bump when changed metadata reuses a version. This detects local metadata changes, not asset-only edits or releases made elsewhere. Global release uniqueness requires a publishing registry, outside scope. The metadata hash cannot enforce all version bumps.

Source identity is `(UUID, exact version, rootHash)`: UUID/version helps discovery; hash selects the metadata/history snapshot. Preserve conflicting snapshots with the same UUID/version separately by hash and report the conflict. Identical roots cannot distinguish asset-only changes made without a version bump; a complete package digest could, but is not part of metadata comparison.

### Stored representation

Private serialized `TableProvenanceState` on TableComponent contains current UUID, origin kind, optional legacy fingerprint, direct source hashes, and immutable compact credit records. Use Unity-serializable lists and rebuilt lookup dictionaries. Nested authors must also be immutable or defensively copied. Keep state separate from editable Metadata so replacing that object cannot erase ancestry.

Write `table/provenance.json` with `schemaVersion: 1`, `rootHash`, current `tableId`, `kind`, `sourceFingerprint`, `metadataHash`, `creditIndexHash`, `sources`, and `credits`. Current metadata/version come from table/table.json, captured by the exporter at the same time. Sources are sorted unique immediate-source root hashes, each backed by a credit record.

A credit record contains source rootHash, tableId, version, both ordered author lists, and kind/sourceFingerprint for legacy identity. It contains no ancestor metadata, source-edge list, or asset hashes. Credits are a sorted union of direct sources and all compact ancestral records they supplied. Deduplicate by root hash; conflicting records under one hash are invalid. Current authors are not duplicated as an ancestor in their own package.

For A → B → C, C carries a direct reference to B and compact records for B and A. It does not embed B's full metadata, B's edge to A, or A's full metadata. B's exact package supplies the next edge when requested. All recorded credits remain available offline, while complete path verification requires resolving parents. The compact index still grows with unique source snapshots: this is the explicit storage cost of preserving all known credits offline.

Native roots have a UUID and no sources; derived tables have a UUID and sources; legacyScene has a newly assigned UUID and unknown earlier history. Synthetic vpx records have an empty original UUID, available version/credits, and a SHA-256 source-file fingerprint. MemoryImport uses a generated import UUID fingerprint when bytes are unavailable. Never label fingerprints as original UUIDs. Synthetic legacy roots use a separately tagged hash formula below. Existing Unity scenes and VPX inputs still need these migration rules; unreleased VPE files do not need a legacy-package import path.

### Canonical hashing and verification

Use SHA-256 over a dedicated canonical encoding, not serializer output. Begin records with ASCII domain tag, zero byte, and big-endian U32(schemaVersion). Strings are UTF-8 with big-endian U32 byte lengths; counts and signed year values use big-endian 32-bit integers. Normalize null strings/lists to empty; reject null authors and invalid Unicode scalars. Do not otherwise trim, fold case, normalize Unicode, parse dates, or reorder authors. UUIDs are canonical lowercase hyphenated strings and hashes are 32 raw bytes. This survives Unity null-to-empty persistence.

- Metadata hash: domain VPE-METADATA; version, tableName, abbreviation, primary author array, secondary author array, releaseDate, originalReleaseYear, manufacturer. Authors encode name then VPU handle; arrays encode count then elements. Exclude Unity object references/local paths.
- Credit record encoding: rootHash, tableId, version, primary authors, secondary authors, kind, sourceFingerprint. Credit-index hash: domain VPE-CREDITS, count, then records sorted by rootHash bytes.
- Current root: domain VPE-PROVENANCE-NODE; tableId, kind, sourceFingerprint, metadataHash, creditIndexHash, source count, sorted direct-source hash bytes. Version is committed through metadataHash.
- Synthetic legacy root: domain VPE-LEGACY-SOURCE; kind, sourceFingerprint, raw legacy version, primary authors, secondary authors. Its resulting credit record uses that digest as rootHash, with unknown original identity explicit.

The parent hash already commits to its own source references. The child hashes that value directly; it need not download/recompute the parent. Local envelope validation verifies current metadata, direct references, and compact credits against the package root. It cannot independently establish that an inherited record agrees with an unavailable ancestor package.

Expose local-envelope-valid, source-unresolved, source-record-verified, and resolved-history-invalid states separately. When a parent is supplied, recompute its root and compare its identity/version/current credits and inherited credit index against the child's records. Missing sources are unresolved, not corrupt. Mismatches do not overwrite captured snapshots. Hashes do not authenticate authorship: someone can deliberately rewrite data and recalculate them.

Reject duplicate JSON properties, invalid keys/IDs, conflicting records, duplicate sources, missing direct-source records, unsupported schemas, and resource excess. Unresolved deeper graph paths are allowed; remove the earlier full-graph requirement that every edge/node must be present locally. Deferred resolution checks cycles, conflicts, and depth using iterative traversal and a visited hash set. Equality means equal declared metadata and ancestry commitments, not identical assets/gameplay.

Golden fixtures cover Unity reload/null normalization, version changes, Unicode, author roles/order, JSON formatting independence, source/credit-index changes, legacy roots, and reused UUID/version conflicts. Canonical changes require a new provenance schema; never reinterpret old hashes.

### Optional parent retrieval and comparison

Resolve from local imported/installed package indexes and a bounded cache of validated metadata envelopes by root hash. An ITableSourceResolver accepts UUID, version, expectedRootHash and returns an immutable package/envelope or unavailable. Initial support includes local packages and selecting a downloaded parent file. A remote registry adapter can follow; this task has not identified an existing registry that resolves these identities. Do not invent UUID download URLs or automatically fetch arbitrary URLs from a package.

Downloads are optional, user-initiated, cancellable, and never required to import/export/play. Cache envelopes rather than redundant full parent packages. Resolution does not silently change stored credits/current hash; a future evidence-reconciliation command must explicitly account for added evidence.

| Comparison | Meaning |
| --- | --- |
| Equal valid roots | Equal current metadata and recorded ancestry commitments; deeper sources can remain unresolved. |
| Equal metadata hashes, different roots | Current release details match; identity/origin/source references/inherited records differ. |
| Same UUID/version, different roots | Conflicting release declarations; preserve both. |
| Same UUID, different versions | Revisions of one declared identity, not necessarily a derivation. |
| Other root is a direct source | Recorded immediate derivation. |
| Other root is only in the compact index | Reported ancestor; its exact path needs resolution. |
| Shared credit-record hashes | Common recorded source; distinguish from verified resolved paths. |
| No common records | No recorded common ancestry found, not proof of independent creation. |
| Parent unavailable/unsupported | Unresolved/unsupported; basic operations still work. |

### Import, export, migration, and safety

Full VPE editor import splits Setup into preflight and destination creation. Validate the local envelope, copy its credit index, add the source root's UUID/version/credits, set that root as the immediate source, assign a new UUID, and reset current version to 1.0.0. Retain editable title/current credits initially, as today. Attach provenance before committing successful completion. Runtime loading restores current identity without deriving.

Use the existing locked read handle for source reads. Legacy fingerprinting probes with leave-open semantics, hashes that handle to completion on a worker, seeks to zero, then constructs/reads the archive. Never hash a raw stream concurrently with ZIP entry reads. Stage once only where needed. New-format provenance import hashes metadata only; asset verification is separate.

Register the finished root once with Undo.RegisterCreatedObjectUndo after async work; mark its scene dirty and retain extracted assets for Redo. Do not collapse Undo groups spanning yields. Failure removes only the attempt's root/fresh folder, never matched library assets or pre-existing variants. Full VPX imports reserve unique destinations instead of reusing Assets/Tables/<name>; tests use disposable destinations. Existing component-edit conversion retains its destination.

New Table explicitly initializes native provenance. Pre-feature scenes initialize legacyScene and current version 1.0.0 once, with an explicit Undo.RecordObject operation, dirty marking, and prefab overrides where applicable. Preserve any available historical source version separately; do not invent one. Prefer initialization before current metadata edits; export is fallback. Failed export retains initialized identity. Undo initialization is an explicit lifecycle action; Redo restores the UUID. Ordinary saves, renames, reloads, refreshes, and play never derive or increment versions.

Raw Unity duplication/Save As retains identity because intent is unknown. Create derivative explicitly duplicates the root, captures its source hash/credits, assigns new identity/version, and leaves the source unchanged. The read-only inspector displays current UUID/version/hashes, source UUID/version/credits, inherited credits, and resolved/unresolved paths. No ancestry reset/reorder/delete controls or multi-object edits.

Export snapshots metadata/provenance once, validates before touching the destination, writes both payloads consistently, and atomically publishes a temporary sibling package after successful close. Failure preserves the prior file. The same metadata/provenance/version hashes identically regardless of node IDs, ZIP packing, or asset changes. Headless exports use the same service.

VPX preserves InfoAuthorName as one unsplit primary-credit string, InfoVersion verbatim, and a file fingerprint. Preloaded containers without bytes use silent memoryImport descriptors. Materials-only diagnostic imports retain source markers and cannot masquerade as complete tables during export. Arbitrary disconnected asset copying is outside genealogy. Development VPE artifacts lacking the required metadata/hashes must be regenerated from their source scenes, not silently migrated as anonymous ancestors.

### Format and resource limits

Use container format 2 with required provenance schema 1 and integrity manifest schema 1. VPE is unreleased; update writer, readers, and fixtures together, without a format-1/hashless compatibility path. Development artifacts can be re-exported. Unsupported versions and missing/invalid required metadata fail explicitly rather than dropping materials, stripping ancestry, or becoming empty native/legacy roots.

Use bounded parsing through existing IPackageFile.AsStream and direct ZIP summary streams. Validate required provenance before runtime prefetch and keep it outside unbounded item/ref prefetch. Reject unknown fields in hashed schema-1 records. Initial limits: 8 MiB provenance JSON, 4,096 credit records, 256 direct sources, 32,768 author entries, 16 KiB per string. Preflight the new source record before committing import; never truncate credits to fit. Deferred resolution separately caps depth at 256, nodes at 4,096, and fetched metadata at 32 MiB per operation, returning partial/unresolved status at limits.

Storage is O(C + A), unique credit records plus author bytes. Hashing is linear plus sorting; immutable index digests are cached, so current edits need only rehash current metadata/root. Explicit file comparison verifies current bytes; path/size/mtime is only a UI hint. No hierarchy fetch/hash/traversal on repaint or ordinary library scan. Benchmark 100/1,000-record indexes, shared sources, maximum inputs, and partial resolution, recording time/bytes/allocations. Proposed cold-comparison targets are 50/250 ms on the development machine, to be measured rather than assumed.

## B. Global SHA-256 texture cache and corruption protection

### Scope and identities

Use one per-user VPE cache across table files and player render-pipeline variants, with an optional configured root for editor/player sharing. Resolve a stable VPE application-data location explicitly; different executables' Application.persistentDataPath values need not match. Never place it in a table or asset-library checkout. Sharing across users/machines/networks is outside the first release.

Store GPU-ready derived data. Retain the original VPE as the repair source, without a second permanent encoded-source cache. Byte-identical sources are the deduplication boundary: different PNG encodings of equivalent pixels do not automatically merge. Identical library images may share cooked objects regardless of filenames, table versions, or Unity GUIDs.

SourceHash is SHA256 of exact packaged source bytes, after any export conversion. Every VPE must declare it and its byte length. Missing hashes are a format error; there is no old/hashless VPE fallback. Filenames, texture IDs, table UUID/version, and CRC32 never become content keys. File metadata is only a cheap invalidation signal for previous verification.

CookKey is SHA256 of a separately tagged canonical per-texture recipe. Include SourceHash, decoder/input representation, authored size clamp, effective resolution policy, texture role/normal input packing and output swizzle, mip policy, effective target format/block layout, ForceUncompressed, effective compression policy, and relevant decode/resize/mipmap/repack/encoder algorithm versions. Include backend compatibility where it changes correctness or representation. Snapshot settings once per load. Disk compression is storage policy, not a cook input.

Audit all inputs used by BuildPlan and downstream decode/encode, with tests varying each. The same bytes used as color and normal data require distinct variants when processing differs. If one package ID serves incompatible roles, use internal `(textureId, recipe)` bindings and route references accordingly: the existing global normal-ID set cannot safely represent both uses. Apply normal packing rewrites only to references using the corresponding variant.

Exclude sampler state, source names, package IDs, unrelated material scalars, and table identity unless they affect generated pixels. Wrap/filter/aniso remain current binding state. ColorSpace is not read by today's BuildPlan and its intermediate textures are linear; audit the encoder and downstream stages, and include color space in CookKey only if it affects generated bytes. Otherwise sRGB/linear bindings share cooked bytes while retaining their own upload configuration. Future color-aware mip/encode changes require a recipe version bump and the relevant color input. Optional in-memory Texture2D sharing needs a separate instance key including mutable sampler/color configuration and reference-counted lifetime. Disk sharing still requires allocation/upload after a process restart.

### Mandatory integrity manifest and first-use verification

Add required `integrity.json`, schema 1, listing every other file entry by exact normalized package path, uncompressed byte length, and SHA-256 of its uncompressed bytes. Include metadata, provenance, materials, meshes/GLB, sounds, scripts where present, and textures, including editor-only data. The manifest excludes itself to avoid a self-hash cycle. Hash its exact bytes separately in the local verification receipt; an optional distribution-provided whole-archive digest covers the manifest and ZIP structure during download. Keep package-integrity identity separate from the ancestry root, which intentionally excludes assets.

Reject duplicate/colliding paths, traversal, duplicate JSON fields, missing/extra file entries, invalid digests, unsupported schemas, and inconsistent lengths before using the package. Directory entries carry no content and are handled explicitly. Bound manifest size, entry count, per-entry/total uncompressed sizes, and decompression work; define those numeric budgets from the largest supported table fixtures before implementation is considered complete. Do not rely on ZIP CRC32 as the SHA-256 identity. Include every source attribute needed to construct texture recipes in a required texture input descriptor, linked to the corresponding manifest entry. Validate descriptors against decoded headers on first verification, so warm lookup needs no texture-header reads.

Download into a temporary file; check expected transfer length and, when supplied by the distribution service, the whole-file SHA-256 while receiving bytes. On first admission of a downloaded or manually opened package, stream every manifest-listed entry once, verify its digest/length, and validate required schemas before declaring it fully verified. This includes textures already present in the cooked cache: first admission verifies the new archive's embedded bytes, while avoiding their decode/cook work beyond necessary header validation. Integrate entry verification with initial loading/preparation to avoid duplicate reads, using bounded buffers rather than retaining every source. Download verification alone does not establish that the manifest agrees with all embedded entries. An unknown download service or missing external digest does not block internal consistency verification.

After verification, atomically publish a versioned, checksummed local receipt with the manifest digest, normalized path, file identity where available, size, high-resolution modification time, ZIP directory summary digest, verification time, and verifier version. Store an expected archive digest when available, distinguishing externally supplied from locally computed values. Receipt publication is the final step; a crash, failure, or cancellation cannot leave a partly checked package marked verified. Use stable read handles with write exclusion where supported. Otherwise compare file state before/after verification and discard results if it changes. Receipts are rebuildable metadata, not a second copy of source content.

VPE-owned installed files are written through staging and atomic replacement, never modified in place. Externally selected files remain supported; check their receipt on every open. Missing/corrupt receipts, changed identity/size/time/directory/manifest, uncertain file state, or verifier incompatibility trigger first-use verification again. Renames may conservatively trigger verification; shared cooked objects survive. File watchers are hints, not the only check. These cheap checks detect ordinary replacement/editing, not silent payload bit rot that leaves all checked metadata unchanged; the read-time checks and scrub below cover that gap.

### Warm loading and objects

Use immutable sharded objects such as `TextureCache/v2/objects/<first-two-hex>/<cookKey>`. Headers include format version, complete recipe/key, output dimensions/format/mip layout/packing, bounded lengths, and SHA-256 over the canonical output descriptor plus uncompressed cooked bytes. Support raw or independently chunk-compressed storage. Validate sizes/layout before allocation. Package-local IDs and sampler values are not universal object descriptors.

1. Check the package receipt and small current manifest/directory data. Required metadata still receives bounded parsing/schema validation; unchanged package payloads, including meshes/audio, do not undergo bulk SHA-256 verification again on ordinary runtime loads. Construct recipes from previously verified descriptors and declared SourceHash values, without opening texture source entries. Read a rebuildable per-package load plan keyed by manifest digest, recipe/settings, runtime version, and GPU compatibility if profiling justifies it; this stores references/descriptors, never duplicate cooked payloads. Editor asset matching remains a separate operation with the content checks in section C.
2. Resolve distinct cook keys with bounded I/O concurrency. For each hit, check its persisted object-verification receipt against the opened file's identity/generation, byte length, modification time, small header digest, expected recipe, and verifier version. When unchanged, read/decompress and upload without computing a full payload checksum. Preserve bounds/layout checks, exact read-length checks, and decompressor error handling. Remap objects to current package IDs/bindings. An unchanged warm load performs zero bulk SHA-256 work, including on cooked textures.
3. For misses or corrupt objects, read the corresponding source entry and verify its SHA-256/length again while preparing the cook input. This catches source damage since admission before it can poison a new object. Reject mismatches; do not cache or render unverified fallback bytes. Cook only these recipes, on the main thread where GPU work requires it, then atomically publish verified results.
4. Coalesce repeated recipes and concurrent requests. Supply mixed hits/misses through per-texture handles or bounded batches. Refactor source/cook/prefetch APIs so hits never build the full source blob. A transitional upload blob must not become a second persistent per-table cache; final memory usage is bounded by active batches rather than all source plus cooked bytes.
5. Preserve verified-source fallback for unsupported compute/inputs and cache-unavailable conditions. Encode passthrough versus cooked output explicitly: passthrough still requires source reading/decoding and is not eligible for the zero-source-work warm-load claim. Incompatible GPU formats select compatible variants instead of repeatedly using unusable objects.

Compute the cooked descriptor/payload digest during creation. Verify the persisted object once on its first completed read, normally during initial preparation, before recording a durable object-verification receipt. Persist receipts across player restarts, rather than treating every session as first use. Missing/corrupt receipts, changed file/header state, or an incompatible verifier trigger a full check for only that object before use; successful checks renew the receipt, failed checks trigger repair. Receipt creation uses the same stable-handle and atomic-publication rules as package receipts. A recipe-version change produces a different key; ordinary loading never rewrites immutable objects. No per-load payload hash is hidden in the decompression or upload path.

After admission and a successful cache build/verification, warm texture loads perform no source texture I/O, bulk source/cooked-data hashing, decode, resize, normal repack, mip generation, or encoding for cache hits. Small receipt/manifest/header checks, metadata/mesh/audio reads, object decompression where used, GPU upload, and scene construction remain. Texture caching alone cannot guarantee a near-instant playable table. Measure time to first frame and ready-to-play separately, and optimize whichever remaining stage dominates; do not promise a fixed latency without representative tables and hardware.

### Detection and repair without blocking warm loads

| Failure | Detection | Recovery |
| --- | --- | --- |
| Interrupted download/export/cache write | Temporary files never count as committed packages/objects; bounded parsing and length checks reject truncation. | Retain previous valid output; discard abandoned staging files; retry the failed operation. |
| Accidental downloaded/package byte corruption | First-use per-entry verification; expected archive digest when available. | Reject admission and report the affected entry; repair/redownload the same expected package, not an arbitrary version sharing its UUID. |
| Truncated/replaced cooked object or damaged descriptor | File/receipt/header/layout checks; full checksum when changed or not yet verified. | Quarantine the failed object generation, rebuild just that recipe from verified original bytes, then atomically replace; preserve other objects. |
| Silent cooked-payload bit flip with unchanged file/header state | Explicit or idle full verification; normal decompression may detect some damage but is not an integrity guarantee. | On detection, invalidate that object's receipt and repair only that generation. A warm load may use damaged data before detection; this is the accepted tradeoff for no per-load bulk hashing. |
| Changed original VPE | Cheap receipt comparison at open. | Reverify the changed package and update the receipt; reuse all compatible global objects. |
| Silent damage in an unused original texture after admission | Explicit full verification or bounded idle scrub; otherwise detect when the source is next consumed. | Mark the receipt damaged; retain valid cooked objects; offer repair/redownload of the original. Do not claim the archive remains healthy just because rendering succeeds. |
| Corrupt receipt/load-plan/LRU index | Version/length/checksum validation and referenced-object validation. | Discard/rebuild the affected metadata; never wipe valid payload objects solely because an index is lost. |

Write data to unique staging files in the destination filesystem, flush data before publication, and use atomic rename/replace. Publish indexes/receipts only after referenced data is committed. Where supported, flush directory metadata for durable rename; do not assume atomicity alone guarantees persistence through power loss. On restart, incomplete/missing objects are misses, never accepted successes. Keep exports/downloads and cooked objects under separate ownership; clearing the derived cache must not delete original packages.

Coordinate per key in-process. Across processes, stage independently with one validated winner. Use generation-aware leases while reading/repairing/evicting, so a repair cannot remove a newer valid writer's replacement. Permit at most one automatic rebuild attempt per recipe per load; repeated mismatch/failure produces a clear error or verified-source fallback, not a retry loop. Source corruption cannot be fixed by repeatedly cooking the same damaged input. Checksums detect corruption; recovery needs the retained original or another valid copy. Do not add parity files or duplicate every object in the first release.

A recipe is a derivation address, not necessarily the hash of resulting bytes. Compatible GPU implementations may emit different encoded bytes; verify the winner's payload digest on first acceptance, then retain its receipt. Incompatible output requires another recipe/version. Cache hashes and local receipts address accidental corruption, not coordinated malicious rewriting of both bytes and hashes. Publisher authenticity would require a trusted external digest/signature and remains separate from ancestry claims.

Provide an explicit Verify package/cache integrity action, plus optional budgeted idle scrubbing. Full verification bypasses receipts and streams all manifest entries or selected cache objects, renewing receipts only after successful checks. Idle work records last-checked progress, respects byte/I/O budgets, pauses while loading/playing, and can cancel without invalidating good data; it never runs a full scan as a prerequisite to ordinary warm loading. A proposed policy is to inspect oldest-unchecked data while idle, with cadence/budgets chosen from measurements, not rehash all installed tables at each startup. With idle checks disabled, silent unchanged-file corruption remains undetected until explicit verification or another invalidation causes a check.

### Space, migration, and qualification

Provide a byte quota, clear-cache control, and least-recently-used eviction, with a proposed 10 GiB default to validate against real tables. Use leases for active reads/writes including cross-process use; batch recency updates and clean abandoned temps after a grace period. Bound receipt/load-plan/index storage and decoded/cooked bytes in flight. Eviction can make a later load slower but does not invalidate already uploaded textures or remove original VPEs. Consider an explicit per-table pin only if a guaranteed cache residency requirement emerges; do not silently exceed quota to promise instant loads.

Retire development per-table .vptc blobs without a migration reader or seeding path. Their CRC-based identity cannot establish which SHA-256 source produced their bytes; recook once from a regenerated supported VPE, then reclaim old blobs through cleanup. Moving a package or changing unrelated metadata must not invalidate compatible global objects. Measure small-file overhead against the existing sequential cache read. If necessary, add global packed-object segments with a rebuildable hash/offset index and compaction, rather than per-table bundles that duplicate payloads.

Add shared streaming-hash/bounded-parser primitives, the required integrity/texture descriptor schema, verification receipts, recipe/result types, object storage, repair, and quota services. Update writer/readers/fixtures together for the unreleased format. Retain material schema 2 if its structure is unchanged; its reader currently skips unknown versions, so unsupported required schemas must now fail explicitly rather than silently lose materials. Compute export hashes over final bytes after VpeTextureBlobSource.Load conversions and as other entries are written. The manifest is written after collecting those entry digests. Local cache formats evolve independently of provenance.

Refactor PrefetchMaterialData, RestoreMaterialProfilesAsync, VpeTextureSources.Load, VpeTextureCook.CookAsync/BuildPlan, ReplaceEntries, RewriteNormalRefs, and VpeMaterialReader around selected recipes and current-package bindings. Keep core/runtime code independent of editor AssetBrowser services. Preserve editor-only sources in packages and integrity verification, but skip their runtime texture cooking. Images embedded in table.glb are protected by its entry hash but outside sidecar texture deduplication in the first release.

Benchmark first download/admission, first cook/object verification, restart with warm cache, same-process reload, first updated-table admission with one changed texture, and another table sharing library textures. Record source/cooked bytes read, payload bytes hashed separately from small metadata hashes, hash CPU time, hits/misses, decode/encode jobs, upload/scene time, wall time to first frame/playable, memory peak, disk footprint, and file-open overhead. Require zero source texture reads and zero bulk payload hashes for unchanged verified warm hits, zero recooking for unchanged recipes, and bounded recovery for one detected corrupt object. Assert this after a process restart as well as within a session. Measure one-time package/object verification separately and include it in first-load totals. Target warm-load regression below 10% versus the existing cache on the same storage, while pursuing lower overall load latency; this is a proposed qualification target, not evidence of near-instant loads. Set an absolute ready-to-play target from representative hardware/table baselines before implementation is declared performance-complete.

Inject bit flips into source entries, manifest, object descriptor/payload, receipt, and index; truncate files; kill writes before/after rename and before receipt publication; simulate disk full, concurrent repair/eviction, same-path replacement, unchanged timestamps with altered payload, and unsupported schemas. Assert detected damage is never uploaded, a bad object repairs only itself, invalid source repair fails safely, and a cache hit does not falsely claim fresh payload verification. Explicitly test that unchanged-state payload damage can escape cheap checks but is detected by a full check; do not assert a corruption-detection guarantee the fast path cannot provide. Test cancellation and missing original files: valid cooked data is not a complete replacement for the package, whose scene/metadata/sounds are still needed.

## C. Asset-library sound matching for VPE and VPX imports

### Incoming hashes and library index

Build an editor audio index across installed library roots, including audio referenced by prefabs rather than only standalone browser entries. Resolve roots from installed package/library metadata, not hardcoded development paths. Persist content identity, asset GUID/local file ID/path, effective importer recipe, and index generation under Library/VPE/AudioContentIndex. Unity references make this project-local, even though digest algorithms are shared.

First key: SHA-256 over exact encoded bytes when the original encoded stream exists. For supported integer PCM WAV also calculate a versioned canonical hash over format tag, channel count/layout, sample rate, bit depth/valid bits, block alignment, sample count, and exact interleaved samples. Parse bounded RIFF chunks rather than assuming 44-byte headers. Ignore only known non-playback wrappers/padding/informational chunks; include meaningful loop/cue data or conservatively decline canonical matching for such/unsupported chunks. No resampling, loudness normalization, float conversion, or perceptual matching. Unsupported encodings can still match exact bytes in library/VPE files and VPX compressed streams, where those original bytes are available.

**VPX imports compute incoming hashes on every import from actual SoundData.** VPX carries no trusted VPE content digest. For supported WAV, hash Wfx and Data using the same canonical format as the library WAV parser. Regenerated GetFileData headers must not prevent a match. For compressed sounds, hash actual encoded bytes. Never substitute UUID/version, name/path, filename existence, or a prior import record for hashing. Stream/cancel where possible and avoid allocating a second WAV buffer merely to hash it.

VPX WAV matching initially supports integer PCM with complete format semantics available in Wfx/Data. VPX's stored WAVEFORMATEX lacks extensible subformat information, and GetFileData regenerates a simplified fmt chunk; unsupported WAV tags therefore have no original encoded file on which to base an exact-byte fallback. Hashing a regenerated wrapper must not authorize a library match. Such sounds take the new-asset import path, with an explicit unsupported-format error if the existing converter cannot safely reconstruct them. Do not silently reinterpret them as PCM or drop them. Float/extensible canonical matching needs a separately specified format-preserving extension.

VPE imports verify actual incoming sound streams against mandatory package entry digests, sharing that work with first-use integrity verification rather than reading/hashing the same bytes twice. Canonical PCM matching still needs the decoded container's format/sample identity. Persist library hashes incrementally through asset/package changes. Do not rehash the entire library for each import: look up candidates, then validate chosen candidates against current file content/importer state, memoizing within an immutable import attempt. GUID/path alone is not evidence. Invalidate on moves/deletes/reimports/package updates/settings changes and provide force rebuild. Asset discovery/object access stays on the main thread; byte hashing runs on workers.

### Matching and import settings

Resolve a verified original-GUID candidate first, then library candidates by exact bytes and supported canonical WAV identity, then compatible previously imported assets, then a new asset. Prefer original GUID among valid equivalents, otherwise configured library priority followed by stable GUID order. Hash-equivalent candidates are interchangeable only when importer recipes also match. Use an explicit per-import source-to-clip map.

Compare effective AudioImporter settings: force-to-mono/normalization, ambisonic, load type/background/preload, compression format/quality, sample-rate policy/override, and platform overrides. SoundMetaPackable currently omits some settings; extend the sound schema with presence/default semantics and update exporter/preprocessor together. Zero quality is not absence. VPE packages capture complete supported intent; VPX uses the explicit effective default profile of its conversion path. Unknown settings must not silently create an incompatible match.

Never change a shared library clip's importer or .meta for one table. If content matches but settings conflict, find a compatible variant or create one reusable project-local variant keyed by audio identity plus recipe. This is the necessary copy exception: one Unity source asset cannot have conflicting importer settings simultaneously. Report the reason. Table-local volume, pitch, balance, fade, routing, and mappings remain separate and do not require duplicate clips.

Unavailable library/index failures fall back to ordinary import with a summary, never drop audio. A fully matched import writes no destination sounds and triggers no library reimport. Reused GUIDs whose bytes changed must fail candidate verification. Cancellation/cleanup deletes only new owned assets, never matched library clips or prior variants.

### Binding, original names, and re-export

Inject an editor resolver into PackagedFiles.UnpackSounds without adding editor/library dependencies to runtime playback. Map each original package sound GUID to the chosen AudioClip, even when GUIDs differ; multiple incoming GUIDs may map to one clip. Register VpeSoundImportPreprocessor only for new assets. Re-export reads actual selected source files and embeds them, so recipients do not need the library.

In VPX ExtractSounds, replace the final destination-filename lookup with the resolution map. SkipExistingSounds must not bypass content verification: a same-named different file cannot be reused. Retain source Name, InternalName, format/path context, output target, volume, balance, and fade independently of the shared clip. Never rename the shared AudioClip to fit the table.

Add serialized table-local source name to LegacySound; Name and ToSound use it, with clip-name fallback for existing scenes. Update SoundManager/serialization tests so two original names can reference one clip without collapsing entries. ToSound must use actual WAV chunks/format rather than bytes.Skip(44) and forced 16-bit PCM: rematching makes valid library WAVs with extra chunks or other PCM layouts newly reachable here. Preserve supported samples/format or explicitly reject unsupported VPX export, never write corrupt audio. Keep compressed-source handling separate.

Library matches create normal Unity package/project dependencies. Show reused assets/library versions in the import summary, preserve dependency tracking, and never point scene references at evictable disk-cache objects. Removing/updating a library can change/break references like any shared Unity asset; validation can compare stored source identities and report changes. Exported VPE packages remain self-contained. Undo/Redo restores references without deleting shared assets.

### Sound integration and qualification

Add a pure bounded audio identity helper, editor index/resolver, importer recipe model, and report. Extend sound export metadata and both import adapters; fix LegacySound naming/WAV handling as required by reuse. Exercise real trough/bumper/slingshot/plunger library sounds without modifying the library package.

Cover different filenames/GUIDs with identical bytes; VPX rebuilt WAV headers; different informational chunks; one sample changed; different rate/channels; unsupported WAV variants; loop/cue differences; compressed audio; settings conflicts; duplicate sounds; changed content under a GUID; stale index; missing library; renamed clips; two source names sharing a clip; Undo/Redo; cancellation; and export/load without the library. Assert zero writes/reimports for matches, one import per miss/recipe, no library mutation, and no filename-only matching.

Measure first index build separately from warm imports. Warm work is O(incoming bytes + selected candidate verification bytes + sound count), without full-library rescans. Track computed hashes, matches by method, avoided bytes/writes/imports, index I/O, and elapsed time. Sound matching is independent of ancestry downloads and texture cache availability.

## Delivery and acceptance

1. Implement canonical hashes/bounded streams and independent golden fixtures. Specify compact provenance, mandatory package integrity, and audio identities; regenerate development VPE fixtures and assert rejection of missing hashes/unsupported formats.
2. Implement table UUID/version, immutable compact credits, lifecycle/Undo, format-2 preservation, comparison states, and local parent resolution.
3. Implement audio index/resolver, complete import intent, both adapters, original-name preservation, and safe WAV round trips. Verify no-copy library matching and portable re-export.
4. Implement mandatory source texture digests, verified-package receipts, per-use recipes, shared objects, mixed-hit loading/cooking/upload, atomic writes, bounded repair/scrubbing, quota/integrity/cancellation, and unsupported-GPU fallback.
5. Qualify in VisualPinball.Unity.Project.Hdrp using existing Packaging/VPT tests; update packaging/import docs and changelog when implementation ships. Workstreams may ship separately behind their own format/feature gates; resource reuse never depends on ancestry downloads.

| End-to-end case | Required outcome |
| --- | --- |
| A version 1.2.0 imported as B, then B as C | New UUID/version sequences; exact source UUID/version/credits; compact records instead of full parent graphs. |
| Parent downloads removed | All credits/import/export/play work; deeper history is unresolved. |
| Wrong parent revision or reused UUID/version | Mismatch/conflict reported without overwriting evidence. |
| Save/reload with null/empty values and Undo credits | Stable hashes and inherited records. |
| Runtime load/repeated export | No new ancestry or implicit version bump. |
| Failed import/export while user edits elsewhere | Clean owned outputs only; prior exports/assets and unrelated Undo survive. |
| Update changes one texture | Hits for unchanged compatible recipes; only changed recipes cook/add objects. |
| First admission of a new/updated VPE | Verify every declared entry once, reuse matching cooked objects, and publish a receipt only after verification succeeds. |
| Verified package/cache loaded again, including after restart | No source texture reads or bulk source/cooked-payload hashes; persisted receipts survive restart and cheap checks guard reuse. |
| Package/object replaced at the same path | File/header/manifest changes trigger the corresponding verification again; unchanged objects remain reusable. |
| Missing required hashes or unsupported development VPE | Explicit format error; regenerate from the source scene rather than use a compatibility fallback. |
| Declared source hash differs from embedded bytes | Reject first admission or later source consumption; no poisoned shared object is published. |
| Source bit rot after admission leaves file metadata unchanged | Warm hits remain fast; source reads or explicit/idle verification detect damage, without claiming unread originals were reverified. |
| One cached texture has detected corruption | Block its upload; rebuild only that recipe from verified original bytes, with bounded retries. |
| Cached texture payload changes without detectable file/header changes | Warm loading skips bulk hashing; explicit/idle verification detects and repairs it later. |
| Crash during object/receipt publication | Previous committed data remains usable; incomplete data never counts as verified. |
| Two tables use a library texture under different IDs | One object per recipe; correct current bindings/normal/sampler state. |
| Same source in different normal/settings roles | Independent variants whenever cooking differs. |
| Same cooked bytes bound as sRGB and linear | Shared disk object if cooking is unchanged; correct separate binding/upload configuration. |
| Concurrent loads/eviction/corruption/disk full/unsupported GPU | No partial reads/shared-data loss; bounded work and fallback. |
| VPE/VPX contains matching library sounds | Actual incoming hashes, reused clips, no copied matches/library mutations. |
| VPX rebuilds WAV headers | Supported equivalent PCM matches; semantic/sample differences do not. |
| Library name differs from VPX sound name | Original table names/behavior survive re-export. |
| Export after reuse, load without library/cache | Embedded sources make the package portable. |

These cases guide regression coverage. Implemented checks and remaining performance qualification are distinguished in the implementation notes above; proposed timing targets are not measured results.
