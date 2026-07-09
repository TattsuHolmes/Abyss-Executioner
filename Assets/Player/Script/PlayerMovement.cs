using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("移動の速さ")]
    public float walkSpeed = 3.0f;     // 歩く速さ
    public float sprintSpeed = 7.0f;   // ダッシュの速さ（スタミナ消費時）
    public float rotationSpeed = 10.0f;// 振り向く速さ

    [Header("スタミナ設定")]
    public float maxStamina = 100f;      // スタミナの最大値
    public float staminaDrainRate = 25f; // 1秒間に減るスタミナ量（4秒で空になる計算）
    public float staminaRegenRate = 15f; // 1秒間に回復するスタミナ量
    public float currentStamina;         // 現在のスタミナ

    [Header("攻撃の設定")]
    public float attackCooldown = 1.0f;

    private CharacterController controller;
    private Animator animator;
    private Transform cameraTransform;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // 前回先生と直した完璧なコード！
        animator = GetComponentInChildren<Animator>();

        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        currentStamina = maxStamina; // ゲーム開始時はスタミナ満タン

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

        // 攻撃の最中は足を止める！
        if (stateInfo.IsName("Attack1") || stateInfo.IsName("Attack2"))
        {
            animator.SetFloat("Speed", 0f);
            controller.Move(Vector3.down * 9.8f * Time.deltaTime);
            return;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(h, 0f, v).normalized;

        float speedParam = 0f;
        float currentSpeed = walkSpeed;

        // 入力がある（移動しようとしている）場合
        if (inputDir.magnitude >= 0.1f)
        {
            // Shiftキーを押していて、かつスタミナが残っているならダッシュ！
            if (Input.GetKey(KeyCode.LeftShift) && currentStamina > 0)
            {
                currentSpeed = sprintSpeed;
                speedParam = 1.0f; // アニメーターには 1.0 (ダッシュ) を送る
                currentStamina -= staminaDrainRate * Time.deltaTime; // スタミナを減らす
            }
            else
            {
                currentSpeed = walkSpeed;
                speedParam = 0.5f; // アニメーターには 0.5 (歩き) を送る

                // ダッシュしていないならスタミナを回復する
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
            // 立ち止まっている時もスタミナを回復する
            if (currentStamina < maxStamina)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
            }
        }

        // スタミナが0以下になったり、最大値を超えたりしないように制限
        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);

        animator.SetFloat("Speed", speedParam);
        controller.Move(Vector3.down * 9.8f * Time.deltaTime);
    }

    void HandleAttack()
    {
        if (Input.GetMouseButtonDown(0))
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            if (!stateInfo.IsName("Attack1") && !stateInfo.IsName("Attack2"))
            {
                animator.SetTrigger("Attack");
            }
            else if (stateInfo.IsName("Attack1"))
            {
                animator.SetTrigger("Attack");
            }
        }
    }
}