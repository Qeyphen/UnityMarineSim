using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class WaterFloater : MonoBehaviour
{
    public float depthBefSub      = 1f;
    public float displacementAmt  = 3f;
    public int   floaters         = 4;
    public float waterDrag        = 0.99f;
    public float waterAngularDrag = 0.5f;
    public WaterSurface water;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponentInParent<Rigidbody>();
        if (water == null)
            water = FindFirstObjectByType<WaterSurface>();
    }

    void FixedUpdate()
    {
        if (rb == null || water == null || floaters <= 0 || depthBefSub <= 0f) return;

        rb.AddForceAtPosition(
            Physics.gravity / floaters,
            transform.position,
            ForceMode.Acceleration
        );

        WaterSearchParameters search = new WaterSearchParameters
        {
            startPositionWS    = transform.position + Vector3.up * 10f,
            targetPositionWS   = transform.position,
            error              = 0.01f,
            maxIterations      = 8,
            includeDeformation = true,
            excludeSimulation  = false
        };

        water.ProjectPointOnWaterSurface(search, out WaterSearchResult result);
        float waterHeight = result.projectedPositionWS.y;

        if (float.IsNaN(waterHeight) || float.IsInfinity(waterHeight)) return;

        if (transform.position.y < waterHeight)
        {
            float displacementMulti = Mathf.Clamp01(
                (waterHeight - transform.position.y) / depthBefSub
            ) * displacementAmt;

            rb.AddForceAtPosition(
                new Vector3(0f, Mathf.Abs(Physics.gravity.y) * displacementMulti, 0f),
                transform.position,
                ForceMode.Acceleration
            );

            rb.AddForce(
                displacementMulti * -rb.linearVelocity * waterDrag * Time.fixedDeltaTime,
                ForceMode.VelocityChange
            );

            rb.AddTorque(
                displacementMulti * -rb.angularVelocity * waterAngularDrag * Time.fixedDeltaTime,
                ForceMode.VelocityChange
            );
        }
    }
}