using UnityEngine;

public class EnemyHitbox : MonoBehaviour
{
    [SerializeField] private Enemy owner;
    [SerializeField] private Enemy.HitArea hitArea = Enemy.HitArea.Body;

    public Enemy Owner => owner;
    public Enemy.HitArea HitArea => hitArea;

    void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<Enemy>();
    }
}
