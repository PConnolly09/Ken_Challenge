using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    private float length, startpos;
    public GameObject cam;

    [Tooltip("1 = Still (Sky), 0 = Moves with Player. Updated to use LateUpdate for smoothness.")]
    public float parallaxEffect;

    void Start()
    {
        if (cam == null) cam = Camera.main.gameObject;
        startpos = transform.position.x;

        if (GetComponent<SpriteRenderer>() != null)
            length = GetComponent<SpriteRenderer>().bounds.size.x;
    }

    // Changed from FixedUpdate to LateUpdate to match camera movement (prevents jitter)
    void LateUpdate()
    {
        float temp = (cam.transform.position.x * (1 - parallaxEffect));
        float dist = (cam.transform.position.x * parallaxEffect);

        transform.position = new Vector3(startpos + dist, transform.position.y, transform.position.z);

        // Infinite Loop Logic
        if (length > 0)
        {
            if (temp > startpos + length) startpos += length;
            else if (temp < startpos - length) startpos -= length;
        }
    }
}