using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] ItemPickupSequence pickupSequence;
    [SerializeField] InteractionPromptUI prompt;
    [SerializeField] float reachDistance = 2.6f;

    Collectible current;
    float currentDistance;

    void Update()
    {
        // También hay que ignorar el forcejeo con la araña: ahí E se machaca para
        // zafarse, y si no se bloquea acá se recogerían objetos de rebote.
        bool busy = (pickupSequence != null && pickupSequence.IsPlaying)
                    || (SpiderCatchSequence.Instance != null && SpiderCatchSequence.Instance.IsPlaying);

        current = busy ? null : FindNearest();
        if (current == null) return;

        // Se pide el cartel cada frame en vez de mostrarlo y ocultarlo por cambios de
        // estado: así varios interactuables cercanos no se lo roban entre sí.
        if (current.Entry != null)
            prompt?.Request("tomar " + current.Entry.entryTitle, currentDistance);

        if (Input.GetKeyDown(KeyCode.E))
            pickupSequence?.Play(current);
    }

    Collectible FindNearest()
    {
        Collectible best = null;
        float bestDist = reachDistance;

        foreach (var c in Collectible.All)
        {
            if (c == null || !c.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
            }
        }

        currentDistance = bestDist;
        return best;
    }
}
