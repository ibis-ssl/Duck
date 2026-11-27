# I81-05B2 evidence

This bundle preserves the focused model test history, the corrected direct model regression, and the AppHost build. The source hashes below identify the final source for this checkpoint.

The pre-product runtime Red was a resource-count assertion (expected 2, actual 5). After the product branch was added, the next assertions exposed test assumptions: Aspire `WithImageSHA256` leaves `Tag` null, and this SDK model exposes an image annotation plus an args callback, not a `ContainerEntrypointAnnotation`. The fixed image repository and exact index digest remain asserted. The argument callback is checked against the locked image's command contract, while the product source explicitly calls `WithEntrypoint("tini")`.

The earlier report that v3/v4 contained `Wsl/Service/E_ACCESSDENIED` was incorrect. Their saved stdout contains actual .NET test output and an `Assert.Single` failure. The WSL error appeared only in a later attempt to read those logs while WSL was unavailable.

Focused test: 1/1 passed. Direct `AppHostApplicationModelTests`: 26/26 passed. AppHost `--no-restore` build: exit 0. These checks validate the AppHost model and build only. No image build, DCP/container start, multicast traffic, or strict health success was measured.

The original `/tmp` generations were copied byte-for-byte here and left intact. The initial Red and v2 raw outputs have no saved `result.json`; this bundle does not reconstruct missing command/tool metadata.
