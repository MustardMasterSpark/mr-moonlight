
using UnityEngine;
namespace Burntwax
{
    public interface Destructible
    {
        public float CurrentHealth { get; set; }
        public void TakeDamage(float damage);

        public void Destruct(Vector3 pos);
    }
}
