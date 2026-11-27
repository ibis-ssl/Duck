from pathlib import Path
import ast
root=Path("/home/donabe/Duck-issue81-aspire-desktop-bridge")
for name in ("scripts/tests/verify-debughost-portable-pdb.py", "scripts/tests/test-debughost-portable-pdb.py"):
    ast.parse((root/name).read_text())
print("Python syntax passed for 2 files")
