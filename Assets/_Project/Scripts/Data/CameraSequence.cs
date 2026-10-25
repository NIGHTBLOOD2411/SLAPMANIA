using UnityEngine;
using SlapMania.Core.Cinematics;

namespace SlapMania.Data
{
    /// A recorded camera move as an asset (M16, ADR-0020). Usually created by
    /// `Tools > Slap Mania > Camera > Import Recorded Sequences` from the runtime recorder's
    /// JSON; also editable here. `CameraDirector` plays it on a `CameraCue`.
    [CreateAssetMenu(menuName = "Slap Mania/Camera Sequence", fileName = "CameraSequence")]
    public sealed class CameraSequence : ScriptableObject
    {
        public CameraSequenceData data = new CameraSequenceData();

        private void OnValidate()
        {
            if (data == null) return;
            CameraTrack.Sort(data.keys);
        }
    }
}
