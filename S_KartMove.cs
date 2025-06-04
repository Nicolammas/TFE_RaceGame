using System.Collections;
using Debugging;
using UnityEngine;
using DG.Tweening;
using Player.Movement;
using Unity.VisualScripting;
using UnityEngine.Rendering;

[RequireComponent(typeof(Rigidbody))]
public class S_KartMove : MonoBehaviour
{
    private float horizontalInput, verticalInput;
    private float currentSteerAngle, currentbreakForce;
    private bool isBreaking;
    private GameObject currentDriver;
    private Rigidbody rb;

    [Header("Base settings")]
    [SerializeField] private float collisionPushBackForce = 1f;
    [SerializeField] private float collisionJumpForce = 5f;
    [SerializeField] private float motorForce, breakForce, maxSteerAngle;
    [SerializeField] private float uprightStrength = 5f;

    public bool HasDriver => currentDriver != null;
    public int OwnerID;

    public int GetOwnerID()
    {
        return OwnerID;
    }

    public void SetOwnerID(int value)
    {
        OwnerID = value;
    }

    [Header("Drift Settings")]
    [SerializeField] private float driftStiffnessFactor = 0.6f;
    [SerializeField] private float normalRearStiffness = 2.0f;
    [SerializeField] private GameObject driftParticulesFL, driftParticulesFR, driftParticulesBL, driftParticulesBR;
    [SerializeField] private TrailRenderer driftMarkRight, driftMarkLeft;
    [SerializeField] private Material driftVignetteMat;
    private WheelFrictionCurve normalRearFriction;

    [Header("Drift Boost Settings")]
    [SerializeField] private float minDriftDurationForBoost = 1.0f;
    [SerializeField] private float driftBoostForce = 2000f;
    [SerializeField] private float maxAlignmentAngleForBoost = 15f;
    [SerializeField] private float boostDelayWindow = 0.5f;
    private float driftStartTime;
    private bool isDrifting;

    [Header("Collision Settings")]
    [SerializeField] private GameObject collisionVFXPrefab;

    [Header("Steering Control")]
    [SerializeField] private float steerFactor = 1f;
    [SerializeField] private float driftSteerFactor = 0.6f;

    [Header("Dotwenn Animation")]
    [SerializeField] private float driftTiltAngle = 10f;
    [SerializeField] private float tiltDuration = 0.2f;
    [SerializeField] private float accelShakeDuration = 1f;
    [SerializeField] private Vector3 accelShakeStrength = new Vector3(0.03f, 0.03f, 0.03f);
    [SerializeField] private int accelShakeVibrato = 10;
    private Tween accelTween;
    private GameObject kartMesh;

    [Header("wheels parameters")]
    [SerializeField] private WheelCollider frontLeftWheelCollider;
    [SerializeField] private WheelCollider frontRightWheelCollider;
    [SerializeField] private WheelCollider rearLeftWheelCollider, rearRightWheelCollider;
    [SerializeField] private Transform frontLeftWheelTransform, frontRightWheelTransform;
    [SerializeField] private Transform rearLeftWheelTransform, rearRightWheelTransform;

    public void SetDriver(GameObject driver)
    {
        currentDriver = driver;
    }
    public GameObject GetDriver() => currentDriver;
    public void RemoveDriver()
    {
        StopKart();
        currentDriver = null;
        StartCoroutine(ResetKartOrientation());
    }
    private Vector2 playerInput;
    private bool driftInput;
    private bool canCollide;
    private float collisionCooldown = 1f;
    public bool CanCollide { get { return canCollide; } set { canCollide = value; } }

    public void SetInput(Vector2 input, bool isDrifting)
    {
        playerInput = input;
        driftInput = isDrifting;
    }

    public void ResetInput()
    {
        playerInput = Vector2.zero;
        driftInput = false;
    }
    private void Awake()
    {
        kartMesh = transform.GetChild(0).gameObject;
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
    }

    private void Start()
    {
        canCollide = true;
        normalRearFriction = rearLeftWheelCollider.sidewaysFriction;
        normalRearFriction.stiffness = normalRearStiffness;
        rearLeftWheelCollider.sidewaysFriction = normalRearFriction;
        rearRightWheelCollider.sidewaysFriction = normalRearFriction;
    }

    private void FixedUpdate()
    {
        if (currentDriver == null) { ApplyCoast(); return; }
        GetInput();
        HandleMotor();
        HandleSteering();
        UpdateWheels();
        KeepKartUpright();
        ClampAngularVelocity();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!canCollide || collision.gameObject.CompareTag("Ground"))
            return;

        Vector3 normal = collision.contacts[0].normal;
        Vector3 contactPoint = collision.contacts[0].point;
        Vector3 pushDirection = (normal + Vector3.up * 0.5f).normalized;

        bool hasRepelledSomething = false;

        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Collision avec joueur");
            
            var playerController = collision.transform.GetChild(0).GetChild(1).GetComponent<SCR_Player_MovementController>();
            
            if (playerController != null && playerController.PlayerID != OwnerID)
            {
                Debug.Log("Collision avec joueur adverse");
                playerController.ApplyKnockback(-pushDirection * 18);
                hasRepelledSomething = true;
            }
        }

        if (hasRepelledSomething || !collision.gameObject.CompareTag("Player"))
        {
            rb.AddForce(pushDirection * collisionPushBackForce, ForceMode.Impulse);
            rb.AddForce(Vector3.up * collisionJumpForce, ForceMode.Impulse);

            Instantiate(collisionVFXPrefab, contactPoint + new Vector3(0, 0.5f, 0), Quaternion.Euler(-90, -90, 0));
            StartCoroutine(CollisionCooldownCoroutine());
            S_AudioManager.Instance.PlayKartCrash();
        }
    }

    private IEnumerator CollisionCooldownCoroutine()
    {
        canCollide = false;
        yield return new WaitForSeconds(collisionCooldown);
        canCollide = true;
    }

    private void GetInput()
    {
        horizontalInput = playerInput.x;
        verticalInput = playerInput.y;
        isBreaking = driftInput;
    }

    private void HandleMotor()
    {
        float currentSpeed = rb.linearVelocity.magnitude;
        float inputForce = verticalInput != 0f ? verticalInput : 0f;
        float speed = rb.linearVelocity.magnitude;

        if (Mathf.Approximately(inputForce, 0f) && currentSpeed > 0.1f)
        {
            float autoTorque = Mathf.Clamp(currentSpeed / 10f, 0.05f, 0.2f);
            float forwardDir = Vector3.Dot(rb.linearVelocity, transform.forward) >= 0f ? 1f : -1f;
            inputForce = autoTorque * forwardDir;
        }

        frontLeftWheelCollider.motorTorque = inputForce * motorForce;
        frontRightWheelCollider.motorTorque = inputForce * motorForce;

        currentbreakForce = isBreaking ? breakForce : 0f;
        ApplyBreaking();

        if (speed > 0.1f)
        {
            if (accelTween == null || !accelTween.IsActive())
            {
                accelTween = kartMesh.transform.DOShakeScale(accelShakeDuration, accelShakeStrength, accelShakeVibrato)
                                     .SetLoops(-1, LoopType.Restart);
            }
        }
        else
        {
            accelTween?.Kill();
            kartMesh.transform.localScale = Vector3.one;
        }

        bool driftInput = isBreaking && Mathf.Abs(horizontalInput) > 0.1f;
        if (driftInput)
        {
            if (!isDrifting)
            {
                isDrifting = true;
                driftStartTime = Time.time;
                SetRearDriftFriction(driftStiffnessFactor);

                //Drift sound too long
                S_AudioManager.Instance.PlayDrift();

                // start drift VFX
                if (horizontalInput > 0.1f)
                {
                    driftParticulesBR.SetActive(true);
                    driftParticulesFR.SetActive(true);
                    driftMarkRight.emitting = true;
                }
                else if (horizontalInput < -0.1f)
                {
                    driftParticulesFL.SetActive(true);
                    driftParticulesBL.SetActive(true);
                    driftMarkLeft.emitting = true;
                }
            }
        }
        else if (isDrifting)
        {
            EndDrift();
        }
    }

    private void EndDrift()
    {
        isDrifting = false;
        ResetRearFriction();

        float driftDuration = Time.time - driftStartTime;
        if (driftDuration >= minDriftDurationForBoost)
            StartCoroutine(DoBoostAfterAlignment());

        driftParticulesBR.SetActive(false);
        driftParticulesFR.SetActive(false);
        driftParticulesFL.SetActive(false);
        driftParticulesBL.SetActive(false);
        driftMarkLeft.emitting = false;
        driftMarkRight.emitting = false;
    }

    private IEnumerator DoBoostAfterAlignment()
    {
        float timer = 0f;
        while (timer < boostDelayWindow)
        {
            if (rb.linearVelocity.magnitude > 2f)
            {
                float angle = Vector3.Angle(transform.forward, rb.linearVelocity.normalized);
                if (angle < maxAlignmentAngleForBoost)
                {
                    rb.AddForce(transform.forward * driftBoostForce, ForceMode.Impulse);
                    yield break;
                }
            }
            timer += Time.deltaTime;
            yield return null;
        }
    }

    private void KeepKartUpright()
    {
        Vector3 uprightDir = Vector3.up;
        Quaternion desiredRotation = Quaternion.FromToRotation(transform.up, uprightDir) * transform.rotation;
        rb.MoveRotation(Quaternion.Slerp(transform.rotation, desiredRotation, uprightStrength * Time.fixedDeltaTime));
    }

    private void SetRearDriftFriction(float factor)
    {
        WheelFrictionCurve driftFriction = rearLeftWheelCollider.sidewaysFriction;
        driftFriction.stiffness = normalRearFriction.stiffness * factor;
        driftFriction.extremumSlip = 1.5f;
        driftFriction.asymptoteSlip = 2.0f;

        rearLeftWheelCollider.sidewaysFriction = driftFriction;
        rearRightWheelCollider.sidewaysFriction = driftFriction;
    }

    private void ResetRearFriction()
    {
        WheelFrictionCurve normalFriction = rearLeftWheelCollider.sidewaysFriction;
        normalFriction.stiffness = normalRearStiffness * 1.2f;
        rearLeftWheelCollider.sidewaysFriction = normalRearFriction;
        rearRightWheelCollider.sidewaysFriction = normalRearFriction;
    }

    private void ApplyBreaking()
    {
        if (isDrifting)
        {
            frontLeftWheelCollider.brakeTorque = 0f;
            frontRightWheelCollider.brakeTorque = 0f;
            rearLeftWheelCollider.brakeTorque = 0f;
            rearRightWheelCollider.brakeTorque = 0f;
            return;
        }

        frontRightWheelCollider.brakeTorque = currentbreakForce;
        frontLeftWheelCollider.brakeTorque = currentbreakForce;
        rearLeftWheelCollider.brakeTorque = currentbreakForce;
        rearRightWheelCollider.brakeTorque = currentbreakForce;
    }

    private void HandleSteering()
    {
        float lean = isDrifting
            ? -Mathf.Sign(horizontalInput) * driftTiltAngle
            : 0f;
        kartMesh.transform.DOLocalRotate(new Vector3(0, 0, lean), tiltDuration).SetEase(Ease.OutCubic);

        float sp = rb.linearVelocity.magnitude;
        float factor = Mathf.Lerp(1.5f, 0.8f, Mathf.Clamp01(sp / 10f));
        currentSteerAngle = maxSteerAngle * horizontalInput * (isDrifting ? driftSteerFactor : steerFactor) * factor;
        frontLeftWheelCollider.steerAngle = currentSteerAngle;
        frontRightWheelCollider.steerAngle = currentSteerAngle;
    }

    private void UpdateWheels()
    {
        UpdateSingleWheel(frontLeftWheelCollider, frontLeftWheelTransform);
        UpdateSingleWheel(frontRightWheelCollider, frontRightWheelTransform);
        UpdateSingleWheel(rearRightWheelCollider, rearRightWheelTransform);
        UpdateSingleWheel(rearLeftWheelCollider, rearLeftWheelTransform);
    }

    private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform)
    {
        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);
        wheelTransform.rotation = rot;
        wheelTransform.position = pos;
    }

    private void ApplyToWheel(WheelCollider wc, float torque, bool braking)
    {
        wc.motorTorque = torque;
        wc.brakeTorque = braking ? breakForce : 0f;
    }

    private void ApplyCoast()
    {
        if (rb.linearVelocity.magnitude > 0.1f)
        {
            ApplyToWheel(frontLeftWheelCollider, 0, true);
            ApplyToWheel(frontRightWheelCollider, 0, true);
            ApplyToWheel(rearLeftWheelCollider, 0, true);
            ApplyToWheel(rearRightWheelCollider, 0, true);
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void ClampAngularVelocity()
    {
        Vector3 angular = rb.angularVelocity;
        angular.y = Mathf.Clamp(angular.y, -3f, 3f);
        rb.angularVelocity = angular;
    }

    private IEnumerator ResetKartOrientation()
    {
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            kartMesh.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        kartMesh.transform.rotation = targetRotation;
    }

    // Pour quand on rentre a la caisse
    public void StopKart()
    {
        currentDriver = null;
        if (rb.linearVelocity.magnitude > 0.1f)
        {
            ApplyToWheel(frontLeftWheelCollider, 0, true);
            ApplyToWheel(frontRightWheelCollider, 0, true);
            ApplyToWheel(rearLeftWheelCollider, 0, true);
            ApplyToWheel(rearRightWheelCollider, 0, true);
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        EndDrift();
    }
}