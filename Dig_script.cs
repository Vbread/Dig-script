using UnityEngine;

public class Dig_script : MonoBehaviour
{
    public Alt_mine Terrain;
    public float DigRadius = 0.5f, EffectGemRadius = 0.25f;
    public float DigStrength = 0.1f;
    public Transform Parent_TR;
    public Transform FakePick_TR;
    public Vector3 LocalOffset = new Vector3(0.5f, 0, 0.5f);

    public float LaunchForce = 10f;

    public float ResetDelay = 2f;
    private float timeToReset;

    private Rigidbody Digger_RB;

    // Calculate world position based on parent's rotation and position
    private Vector3 WorldPosition => Parent_TR.position + Parent_TR.right * LocalOffset.x + Parent_TR.up * LocalOffset.y + Parent_TR.forward * LocalOffset.z;

    private void Awake()
    {

        this.GetComponent<Collider>().enabled = false;

        Digger_RB = this.GetComponent<Rigidbody>();
        // Set initial position relative to parent
        Digger_RB.position = WorldPosition;
        timeToReset = ResetDelay;
    }

    void Update()
    {
        if (!this.GetComponent<Collider>().enabled)
        {
            // Update position to follow parent's rotation
            Digger_RB.position = WorldPosition;

        }

        if (this.GetComponent<Collider>().enabled)
        {
            timeToReset -= Time.deltaTime;
            if (timeToReset <= 0f)
            {
                this.GetComponent<Collider>().enabled = false;
                Digger_RB.position = WorldPosition;
                timeToReset = ResetDelay;
                Digger_RB.linearVelocity = Vector3.zero;
            }
        }
    }

    public void SetRotation(float rot)
    {
        // Set rotation relative to parent's forward direction
        if (!this.GetComponent<Collider>().enabled)
        {
            Digger_RB.rotation = Parent_TR.rotation * Quaternion.Euler(rot, 0, 0);
        }
    }

    public void TurnOnCollider()
    {
        this.GetComponent<Collider>().enabled = true;

        // Apply force in the parent's forward direction
        Digger_RB.AddForce(Parent_TR.forward * LaunchForce);
    }

    void OnCollisionEnter(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            Terrain.AddCrater(contact.point, DigRadius, DigStrength);
        }

        this.GetComponent<Collider>().enabled = false;

        Collider[] hitColliders = Physics.OverlapSphere(Digger_RB.position, EffectGemRadius);

        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider.GetComponent<Gem>())
            {
                Rigidbody Gem_RB = hitCollider.GetComponent<Rigidbody>();

                Gem_RB.GetComponent<Rigidbody>().useGravity = true;
                Gem_RB.GetComponent<Rigidbody>().isKinematic = false;
                hitCollider.GetComponent<Gem>().Mined();
            }
        }

        Digger_RB.position = WorldPosition;

        Digger_RB.linearVelocity = Vector3.zero;
        FakePick_TR.GetComponent<Rotate_pick>().ResetPickaxe();


    }

    void OnTriggerStay(Collider other)
    {
        Terrain.AddCrater(transform.position, DigRadius, DigStrength * Time.deltaTime);
        this.GetComponent<Collider>().enabled = false;

        Collider[] hitColliders = Physics.OverlapSphere(Digger_RB.position, EffectGemRadius);

        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider.GetComponent<Gem>())
            {
                Rigidbody Gem_RB = hitCollider.GetComponent<Rigidbody>();

                Gem_RB.GetComponent<Rigidbody>().useGravity = true;
                Gem_RB.GetComponent<Rigidbody>().isKinematic = false;
                hitCollider.GetComponent<Gem>().Mined();
            }
        }

        Digger_RB.position = WorldPosition;
        Digger_RB.linearVelocity = Vector3.zero;
        FakePick_TR.GetComponent<Rotate_pick>().ResetPickaxe();

    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        // Calculate world position for gizmo drawing
        Vector3 gizmoPos;
        if (Application.isPlaying)
        {
            gizmoPos = WorldPosition;
        }
        else
        {
            // Handle editor preview when parent might be null
            gizmoPos = Parent_TR != null ?
                Parent_TR.position + Parent_TR.right * LocalOffset.x + Parent_TR.up * LocalOffset.y + Parent_TR.forward * LocalOffset.z :
                transform.position;
        }

        Gizmos.DrawWireSphere(gizmoPos, 0.1f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(Digger_RB.position, DigRadius);
    }
}