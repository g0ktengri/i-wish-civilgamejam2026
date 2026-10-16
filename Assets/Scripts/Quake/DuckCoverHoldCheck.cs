namespace Deprem.Quake
{
    /// <summary>
    /// Çök-kapan-tutun kuralı (GDD): uyarı çıktıktan sonra kısa bir başlama süresi var; ondan sonra
    /// C sarsıntı boyunca basılı tutulmalı. Kısa bir bırakma toleransı aşılırsa başarısız.
    /// </summary>
    public sealed class DuckCoverHoldCheck
    {
        private readonly float grace;
        private readonly float tolerance;
        private float elapsed;
        private float notHoldingTime;
        private bool everHeld;

        public bool Failed { get; private set; }
        public bool IsHolding { get; private set; }

        public DuckCoverHoldCheck(float grace, float tolerance)
        {
            this.grace = grace;
            this.tolerance = tolerance;
        }

        public void Update(float dt, bool holding)
        {
            if (Failed) return;
            elapsed += dt;
            IsHolding = holding;
            if (holding)
            {
                everHeld = true;
                notHoldingTime = 0f;
                return;
            }

            // Başlama süresinde henüz basmamak sorun değil
            if (!everHeld && elapsed < grace) return;
            notHoldingTime += dt;
            if (notHoldingTime > tolerance) Failed = true;
        }
    }
}
