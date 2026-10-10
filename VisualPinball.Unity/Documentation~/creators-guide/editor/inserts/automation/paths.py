"""Where things are, from this folder: the workspace holds VisualPinball.Engine, VisualPinball.Unity.Hdrp and vpe-terminator2 side by side."""
import os, tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
WORKSPACE = Path(os.environ.get('VPE_WORKSPACE', HERE.parents[6]))
DOCS = HERE.parent
T2 = WORKSPACE / 'vpe-terminator2'
LOOKDEV = WORKSPACE / 'VisualPinball.Unity.Hdrp' / 'LookDev~'
# Scratch output of the scripts (previews, stitched intermediates): left out of the docs.
WORK = Path(os.environ.get('VPE_AUTOMATION_WORK', Path(tempfile.gettempdir()) / 'vpe-automation'))
WORK.mkdir(parents=True, exist_ok=True)


def output(project, tool):
	"""A project's automation output folder: <project>/Library/Automation/<tool>."""
	folder = project / 'Library' / 'Automation' / tool
	folder.mkdir(parents=True, exist_ok=True)
	return folder
