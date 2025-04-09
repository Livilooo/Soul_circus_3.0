using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 5f;
    public float mouseSensitivity = 2f;

    // This flag controls whether the player can move.
    public bool canMove = true;

    [Header("Camera Settings")]
    public float fixedCameraXRotation = 0f; // Fixed X rotation for the camera.

    private Rigidbody rb;
    private Camera playerCamera;
    private float yRotation = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerCamera = Camera.main;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Set the camera's fixed X rotation at the start.
        playerCamera.transform.localRotation = Quaternion.Euler(fixedCameraXRotation, 0f, 0f);
    }

    void Update()
    {
        // Check for "C" key press and trigger active buttons.
        HandleUIButtonActivation();

        // Only process input if movement is allowed.
        if (!canMove)
            return;

        HandleMouseLook();
        HandleMovement();
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;

        yRotation += mouseX;

        // Rotate the player around the Y-axis based on mouse input.
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        // Keep the camera's X rotation fixed.
        playerCamera.transform.localRotation = Quaternion.Euler(fixedCameraXRotation, 0f, 0f);
    }

    void HandleMovement()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        move.Normalize();

        Vector3 velocity = move * moveSpeed;
        velocity.y = rb.velocity.y;

        rb.velocity = velocity;

        if (IsGrounded() && Input.GetButtonDown("Jump"))
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    bool IsGrounded()
    {
        RaycastHit hit;
        return Physics.Raycast(transform.position, Vector3.down, out hit, 1f);
    }

    void HandleUIButtonActivation()
    {
        // Check if the "C" key is pressed.
        if (Input.GetKeyDown(KeyCode.C))
        {
            Debug.Log("C key pressed!");

            // Find all active buttons in the scene.
            Button[] buttons = FindObjectsOfType<Button>();

            foreach (Button button in buttons)
            {
                if (button.gameObject.activeInHierarchy && button.interactable)
                {
                    Debug.Log($"Activating button: {button.name}");
                    button.onClick.Invoke();
                }
            }
        }
    }
}