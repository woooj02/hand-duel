#if SOH_INPUT_DIAGNOSTICS
using UnityEngine;
namespace ShowOfHands
{
    public sealed partial class App
    {
        float inputDiagnosticAt;
        void PollInputDiagnostics()
        {
            if(Time.unscaledTime<inputDiagnosticAt)return;
            inputDiagnosticAt=Time.unscaledTime+2;
            Debug.Log("SOH_INPUT_DIAG appFocus="+focused+" unityFocus="+Application.isFocused+
                " vrFocus="+OVRManager.hasVrFocus+" inputFocus="+OVRManager.hasInputFocus+
                " screen="+Screen+" pointer="+pointer.Active+" mask="+pointer.HandMask+
                " left="+hands.Left.Tracked+" right="+hands.Right.Tracked+
                " leftController="+hands.Left.IsController+" rightController="+hands.Right.IsController+
                " leftSuppressed="+hands.Left.SelectSuppressed+" rightSuppressed="+hands.Right.SelectSuppressed+
                " "+(xr==null?"no_source":xr.DiagnosticState()));
        }
    }
}
#endif
