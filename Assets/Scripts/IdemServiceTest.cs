using Idem;
using UnityEngine;

public class IdemServiceTest : MonoBehaviour
{
    private void OnEnable()
    {
        Beamable.API.Instance.Then(beamable =>
        {
            Debug.Log($"[DEBUG] Beamable is ready: {beamable}");
            var instance = Beamable.BeamContext.Default.IdemService();
            Debug.Log($"[DEBUG] IdemService instance: {instance}");
        });
    }
}