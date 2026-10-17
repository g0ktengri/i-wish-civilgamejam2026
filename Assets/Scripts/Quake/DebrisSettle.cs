using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Düşen parça yere ya da bedenin üstüne oturunca yerinde donar (kinematik): kayıp gitmez, üst üste yığılan
    /// parçalar birbirini itip dağıtmaz (Play testi 17: yıkıntı üstümüzde kalsın).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DebrisSettle : MonoBehaviour
    {
        [SerializeField] private float minAge = 0.35f;
        [SerializeField] private float stillTime = 0.25f;

        private Rigidbody body;
        private float age;
        private float still;

        private void Awake() => body = GetComponent<Rigidbody>();

        private void FixedUpdate()
        {
            age += Time.fixedDeltaTime;
            if (age < minAge) return;
            bool slow = body.linearVelocity.sqrMagnitude < 0.04f && body.angularVelocity.sqrMagnitude < 0.25f;
            still = slow ? still + Time.fixedDeltaTime : 0f;
            if (still < stillTime) return;
            body.isKinematic = true;
            enabled = false;
        }
    }
}
