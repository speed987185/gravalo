using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SlingshotPlayer : MonoBehaviour
{
    public float launchPower = 15f;
    public float maxDragDistance = 3f;
    public float minDragDistance = 0.5f;
    public float grabRadius = 1.5f;
    public float groundCheckRadius = 0.3f;
    public float launchCooldown = 0.1f;

    public GameObject dotPrefab;
    public int numberOfDots = 8;
    public float startDotSize = 0.3f;
    public float endDotSize = 0.05f;

    public AudioSource audioSource;
    public AudioClip jumpSound;

    public Transform playerLight;

    private Rigidbody2D[] boneRigidbodies;
    private Camera cam;
    private GameObject[] aimDots;

    private bool isDragging;
    private Vector2 dragStartMousePos;
    private Vector2 currentDragVector;
    private Vector2[] boneStartPositions;
    private float nextLaunchTime;

    private void Awake()
    {
        boneRigidbodies = GetComponentsInChildren<Rigidbody2D>();
        boneStartPositions = new Vector2[boneRigidbodies.Length];

        foreach (Rigidbody2D rb in boneRigidbodies)
        {
            
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        cam = Camera.main;

        aimDots = new GameObject[numberOfDots];
        for (int i = 0; i < numberOfDots; i++)
        {
            if (dotPrefab != null)
            {
                aimDots[i] = Instantiate(dotPrefab, Vector3.zero, Quaternion.identity);
                aimDots[i].SetActive(false);
            }
        }
    }

    private void Start()
    {
        
        if (PlayerPrefs.HasKey("SavedPlayerX") && PlayerPrefs.HasKey("SavedPlayerY"))
        {
            float savedX = PlayerPrefs.GetFloat("SavedPlayerX");
            float savedY = PlayerPrefs.GetFloat("SavedPlayerY");
            Vector2 savedPos = new Vector2(savedX, savedY);

            
            Vector2 offset = savedPos - GetAverageCenter();

            
            transform.position = new Vector3(transform.position.x + offset.x, transform.position.y + offset.y, transform.position.z);

            
            for (int i = 0; i < boneRigidbodies.Length; i++)
            {
                boneRigidbodies[i].position += offset;
                boneRigidbodies[i].linearVelocity = Vector2.zero; 
                boneStartPositions[i] = boneRigidbodies[i].position;
            }
        }
    }

    private void Update()
    {
        


        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SavePlayerProgress();
            SceneManager.LoadScene("Menu"); 
        
            return;
        }

        if (cam == null) cam = Camera.main;
        if (Mouse.current == null || boneRigidbodies.Length == 0) return;

        HandleInput();

        if (playerLight != null)
        {
            playerLight.position = GetAverageCenter();
        }
    }

    private void HandleInput()
    {

        if (Mouse.current.leftButton.wasPressedThisFrame && Time.time >= nextLaunchTime)
        {

            if (!IsGrounded()) return;

            Vector2 mousePos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());


            if (Vector2.Distance(mousePos, GetAverageCenter()) <= grabRadius)
            {
                isDragging = true;
                dragStartMousePos = mousePos;
                currentDragVector = Vector2.zero;

                if (dotPrefab != null)
                {

                    foreach (var dot in aimDots) if (dot != null) dot.SetActive(false);
                }


                for (int i = 0; i < boneRigidbodies.Length; i++)
                {

                    boneRigidbodies[i].constraints = RigidbodyConstraints2D.FreezeAll;

                    boneRigidbodies[i].linearVelocity = Vector2.zero;
                    boneStartPositions[i] = boneRigidbodies[i].position;
                }
            }
        }

        else if (Mouse.current.leftButton.isPressed && isDragging)
        {
            Vector2 mousePos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

            Vector2 rawDrag = mousePos - dragStartMousePos;
            currentDragVector = Vector2.ClampMagnitude(rawDrag, maxDragDistance);


            if (dotPrefab != null && currentDragVector.magnitude > 0.05f)
            {
                Vector2 centerPos = GetAverageCenter();
                Vector2 launchDirection = -currentDragVector;

                for (int i = 0; i < numberOfDots; i++)
                {
                    if (aimDots[i] == null) continue;

                    if (!aimDots[i].activeSelf) aimDots[i].SetActive(true);


                    float t = i / (float)Mathf.Max(1, numberOfDots - 1);
                    aimDots[i].transform.position = centerPos + (launchDirection * t);


                    float size = Mathf.Lerp(startDotSize, endDotSize, t);
                    aimDots[i].transform.localScale = new Vector3(size, size, 1f);
                }

            }

        }


        else if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)

        {
            isDragging = false;



            Vector2 mousePos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

            Vector2 rawDrag = mousePos - dragStartMousePos;

            currentDragVector = Vector2.ClampMagnitude(rawDrag, maxDragDistance);


            if (dotPrefab != null)
            {

                foreach (var dot in aimDots) if (dot != null) dot.SetActive(false);
            }



            bool actuallyLaunched = false;

            for (int i = 0; i < boneRigidbodies.Length; i++)
            {


                boneRigidbodies[i].constraints = RigidbodyConstraints2D.None;
                boneRigidbodies[i].WakeUp();


                if (currentDragVector.magnitude >= minDragDistance)
                {

                    boneRigidbodies[i].AddForce(-currentDragVector * launchPower, ForceMode2D.Impulse);
                    actuallyLaunched = true;

                }
            }

            if (actuallyLaunched)
            {

                nextLaunchTime = Time.time + launchCooldown;


                if (audioSource != null && jumpSound != null)
                {

                    audioSource.PlayOneShot(jumpSound);
                }
            }

            currentDragVector = Vector2.zero;
        }
    }

    private Vector2 GetAverageCenter()
    {
        Vector2 center = Vector2.zero;
        foreach (var rb in boneRigidbodies)
        {
            center += rb.position;
        }
        return center / boneRigidbodies.Length;
    }

    private bool IsGrounded()
    {

        foreach (var rb in boneRigidbodies)
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(rb.position, groundCheckRadius);
            foreach (Collider2D col in colliders)
            {

                if (!col.transform.IsChildOf(this.transform))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void OnApplicationQuit()
    {
        
        SavePlayerProgress();
    }

    private void OnDestroy()
    {
        
        SavePlayerProgress();
    }

    public void SavePlayerProgress()
    {
        if (boneRigidbodies != null && boneRigidbodies.Length > 0)
        {
            Vector2 center = GetAverageCenter();

            
            PlayerPrefs.SetFloat("SavedPlayerX", center.x);
            PlayerPrefs.SetFloat("SavedPlayerY", center.y);
            PlayerPrefs.Save();
        }
    }
}