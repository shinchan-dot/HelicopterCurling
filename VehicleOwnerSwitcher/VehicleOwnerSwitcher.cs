
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VehicleOwnerSwitcher : UdonSharpBehaviour
{
    public SaccFlightAndVehicles.SaccEntity[] SaccEntities;

    private float LastTime;

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time - LastTime < 1 || !other.attachedRigidbody || !Networking.LocalPlayer.IsOwner(other.attachedRigidbody.gameObject)) {
            return;
        }

        if (!other.attachedRigidbody.GetComponent<SaccFlightAndVehicles.SaccEntity>()) {
            return;
        }

        foreach (SaccFlightAndVehicles.SaccEntity entity in SaccEntities)
        {
            if (!entity.Occupied)
            {
                Networking.SetOwner(Networking.LocalPlayer, entity.gameObject);
            }
        }

        LastTime = Time.time;
    }
}
