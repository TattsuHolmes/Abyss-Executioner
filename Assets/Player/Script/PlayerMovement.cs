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

    // ★大進化：一時停止（タメ）の機能を追加！
    [Header("3段目の溜め＆踏み込み設定")]
    public float attack3ChargeDuration = 0.5f;     // 何秒間「溜める（一時停止する）」か（現実の秒数）
    public float attack3ChargeAnimTime = 0.2f;     // アニメのどのタイミングで止めるか（0.0〜1.0：剣を振りかぶった時がおすすめ）
    public float attack3DashSpeed = 12.0f;         // 溜め解放後の、前に踏み込むスピード
    public float attack3DashEndTime = 0.6f;        // 踏み込みが終わるアニメのタイミング（0.0〜1.0）

    private float lastAttackTime;
    private int comboStep = 0;

    // 溜めの裏側で使うタイマーとフラグ
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
    }

    void Update()
    {
        HandleMovement();
        HandleAttack();
    }

    void HandleMovement()
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        // 攻撃中の処理
        if (stateInfo.IsName("Attack1") || stateInfo.IsName("Attack2") || stateInfo.IsName("Attack3"))
        {
            animator.SetFloat("Speed", 0f);

            // --- 3段目の「タメ ➔ 踏み込み」スペシャル処理 ---
            if (stateInfo.IsName("Attack3"))
            {
                float playTime = stateInfo.normalizedTime;

                // ① タメるタイミングに到達し、まだタメていない場合
                if (playTime >= attack3ChargeAnimTime && !hasChargedThisAttack)
                {
                    isCharging = true;
                    // ★ここでアニメーションを超スロー（ほぼ一時停止）にする！
                    animator.speed = 0.05f;

                    // タイマーを進める
                    chargeTimer += Time.deltaTime;

                    // 指定した時間（例: 0.5秒）タメたら解放！
                    if (chargeTimer >= attack3ChargeDuration)
                    {
                        isCharging = false;
                        hasChargedThisAttack = true;
                        animator.speed = 1.0f; // ★アニメーションの速度を元に戻す
                    }

                    // タメている最中は動かさずにここで処理終了
                    controller.Move(Vector3.down * 9.8f * Time.deltaTime);
                    return;
                }

                // ② タメが完了した瞬間から、一気に踏み込む！
                if (hasChargedThisAttack && playTime <= attack3DashEndTime)
                {
                    Vector3 dashMove = transform.forward * attack3DashSpeed * Time.deltaTime;
                    controller.Move(dashMove + Vector3.down * 9.8f * Time.deltaTime);
                    return;
                }
            }
            else
            {
                // 1段目・2段目の時はバグらないようにタメ状態をリセットしておく
                ResetChargeState();
            }

            // 足を止める（1、2段目や、3段目の踏み込みが終わった後）
            controller.Move(Vector3.down * 9.8f * Time.deltaTime);
            return;
        }
        else
        {
            // 攻撃が終わって待機や移動に戻った時も、念のため確実にリセットする
            ResetChargeState();
        }

        // --- いつもの移動処理 ---
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
        }

        if (Input.GetMouseButtonDown(0))
        {
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

    // ★追加：アニメーションの速度やタメの状態を初期化する便利関数
    void ResetChargeState()
    {
        isCharging = false;
        hasChargedThisAttack = false;
        chargeTimer = 0f;
        animator.speed = 1.0f; // アニメーション速度を必ず元(等速)に戻す！
    }
}