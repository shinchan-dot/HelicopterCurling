
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

public class OwnerText : UdonSharpBehaviour
{
    public SaccFlightAndVehicles.SaccEntity SaccEntity;
    public Text Text;

    private void Update()
    {
        VRCPlayerApi player = Networking.GetOwner(SaccEntity.gameObject);
        Text.text = player.displayName;
    }
}
