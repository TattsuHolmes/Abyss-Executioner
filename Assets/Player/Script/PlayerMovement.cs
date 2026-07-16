using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("移動の速さ")]
    public float walkSpeed = 3.0f;
    public float sprintSpeed = 7.0f;
    public float rotationSpeed = 10.0f;

    [Header("スタミナ設定")]
    public float maxStamina = 100f;
    public float staminaDrainRate = 25f;
    public float staminaRegenRate = 15f;
    public float currentStamina;

    [Header("攻撃の設定")]
    public float comboWindow = 1.5f;

    [Header("3段目の溜め＆踏み込み設定")]
    public float attack3ChargeDuration = 0.5f;
    public float attack3ChargeAnimTime = 0.2f;
    public float attack3DashSpeed = 12.0f;
    public float attack3DashEndTime = 0.6f;

    // ★大進化2：武器の表示切り替えとタイマーを追加！
    [Header("武器の切り替え設定")]
    public GameObject handSword;        // 右手に持たせる剣
    public GameObject backSword;        // 背中に背負う剣
    public float autoSheatheTime = 3.0f; // 攻撃後、何秒で自動的に背中に戻すか
    private float sheatheTimer = 0f;    // 納刀までのカウントダウン用タイマー

    private float lastAttackTime;
    private int comboStep = 0;

    private bool isCharging = false;
    private bool hasChargedThisAttack = false;
    private float chargeTimer = 0f;

    private CharacterController controller;
    private Animator animator;
    private Transform cameraTransform;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        currentStamina = maxStamina;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // ★ゲーム開始時は「背中に剣がある」状態にする
        EquipSword(false);
    }

    void Update()
    {
        HandleWeaponSheatheTimer(); // ★自動納刀タイマーを動かす
        HandleMovement();
        HandleAttack();
    }

    // ★追加：自動納刀タイマーの処理
    void HandleWeaponSheatheTimer()
    {
        // タイマーが0より大きい（＝今は手に剣を持っている）場合
        if (sheatheTimer > 0)
        {
            sheatheTimer -= Time.deltaTime; // タイマーを減らす

            // 0になったら背中に戻す
            if (sheatheTimer <= 0)
            {
                EquipSword(false);
            }
        }
    }

    // ★追加：剣の表示/非表示を切り替える便利関数
    // trueを渡すと手持ち（抜刀）、falseを渡すと背中（納刀）になる
    void EquipSword(bool inHand)
    {
        if (handSword != null) handSword.SetActive(inHand);
        if (backSword != null) backSword.SetActive(!inHand);
    }

    void HandleMovement()
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        if (stateInfo.IsName("Attack1") || stateInfo.IsName("Attack2") || stateInfo.IsName("Attack3"))
        {
            animator.SetFloat("Speed", 0f);

            if (stateInfo.IsName("Attack3"))
            {
                float playTime = stateInfo.normalizedTime;

                if (playTime >= attack3ChargeAnimTime && !hasChargedThisAttack)
                {
                    isCharging = true;
                    animator.speed = 0.05f;

                    chargeTimer += Time.deltaTime;

                    if (chargeTimer >= attack3ChargeDuration)
                    {
                        isCharging = false;
                        hasChargedThisAttack = true;
                        animator.speed = 1.0f;
                    }

                    controller.Move(Vector3.down * 9.8f * Time.deltaTime);
                    return;
                }

                if (hasChargedThisAttack && playTime <= attack3DashEndTime)
                {
                    Vector3 dashMove = transform.forward * attack3DashSpeed * Time.deltaTime;
                    controller.Move(dashMove + Vector3.down * 9.8f * Time.deltaTime);
                    return;
                }
            }
            else
            {
                ResetChargeState();
            }

            controller.Move(Vector3.down * 9.8f * Time.deltaTime);
            return;
        }
        else
        {
            ResetChargeState();
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(h, 0f, v).normalized;

        float speedParam = 0f;
        float currentSpeed = walkSpeed;

        if (inputDir.magnitude >= 0.1f)
        {
            if (Input.GetKey(KeyCode.LeftShift) && currentStamina > 0)
            {
                currentSpeed = sprintSpeed;
                speedParam = 1.0f;
                currentStamina -= staminaDrainRate * Time.deltaTime;

                // ★追加：ダッシュ（Shift）したら強制的に背中に戻す！
                sheatheTimer = 0f;
                EquipSword(false);
            }
            else
            {
                currentSpeed = walkSpeed;
                speedParam = 0.5f;

                if (currentStamina < maxStamina)
                {
                    currentStamina += staminaRegenRate * Time.deltaTime;
                }
            }

            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            controller.Move(moveDir.normalized * currentSpeed * Time.deltaTime);
        }
        else
        {
            if (currentStamina < maxStamina)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
            }
        }

        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
        animator.SetFloat("Speed", speedParam);
        controller.Move(Vector3.down * 9.8f * Time.deltaTime);
    }

    void HandleAttack()
    {
        if (Time.time - lastAttackTime > comboWindow)
        {
            comboStep = 0;
            animator.ResetTrigger("Attack"); // 暴発防止もバッチリ残してます
        }

        if (Input.GetMouseButtonDown(0))
        {
            // ★追加：攻撃ボタンを押した瞬間、手に剣を表示してタイマーを3秒にセット！
            EquipSword(true);
            sheatheTimer = autoSheatheTime;

            if (comboStep == 0)
            {
                animator.SetTrigger("Attack");
                lastAttackTime = Time.time;
                comboStep = 1;
            }
            else if (comboStep == 1)
            {
                animator.SetTrigger("Attack");
                lastAttackTime = Time.time;
                comboStep = 2;
            }
            else if (comboStep == 2)
            {
                animator.SetTrigger("Attack");
                lastAttackTime = Time.time;
                comboStep = 0;
            }
        }
    }


    void ResetChargeState()
    {
        isCharging = false;
        hasChargedThisAttack = false;
        chargeTimer = 0f;
        animator.speed = 1.0f;
    }
}