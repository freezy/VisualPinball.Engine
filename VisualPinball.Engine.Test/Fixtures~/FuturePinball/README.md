# Future Pinball integration fixtures

The Future Pinball integration tests use five historical Three Angels tables as an external corpus. The table files are intentionally not committed because of their size and redistribution status.

Set `VPE_FPT_FIXTURES` to the directory containing this layout before running the tests:

```text
fp-2008-1.666/3ANGELS.fpt
fp-2012-installed/3 Angels.fpt
fp-2012-ultra-release/Three Angels_ehanc_Slam.fpt
fp-2012-ultra-source/Three Angels.fpt
fp-2013-enhanced/Three Angels_ehanc_Slam.fpt
```

`FuturePinballFixtureCatalog` locks the source size and SHA-256, compound-directory entry and resource counts, element-type distribution, Table Data size, and compressed/decoded script boundaries and hashes. Tests skip this external corpus when the environment variable is absent; unit tests that synthesize malformed streams remain self-contained.

For the workspace research corpus:

```powershell
$env:VPE_FPT_FIXTURES='E:\_vpe-2025\_analysis\three-angels-fp'
dotnet test VisualPinball.Engine.Test/VisualPinball.Engine.Test.csproj --filter FuturePinball
```
