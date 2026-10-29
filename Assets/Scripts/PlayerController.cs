using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 7f; //最大水平移动速度
    public float groundDrag = 7f; //地面阻力

    public float jumpForce = 12f; //跳跃力度
    public float jumpCooldown = 0.25f; //跳跃冷却时间，防止连续触发
    public float airMultiplier = 0.4f; //空中移动倍率(角色在空中的控制力度是地面的多少倍)
    [Min(0f)] public float gravityMultiplier = 2f; //重力倍率
    [Min(0.1f)] public float pushMoveSpeed = 2.5f; //推箱子时的前进速度
    bool readyToJump = true; //是否允许跳跃

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space; //按空格跳跃

    [Header("Ground Check")]
    public float playerHeight = 2f; //角色高度，用于地面检测
    public LayerMask Ground; //地面检测层级
    bool grounded; 

    public Transform orientation; //角色方向参考

    float horizontalInput; //水平方向输入
    float verticalInput; //垂直方向输入

    Vector3 moveDirection; //三维移动方向

    Rigidbody rb; //角色刚体组件
    Animator animator; //角色动画组件
    PushableBox pushedBox; //当前正在推动的箱子
    Vector3 pushDirection; //开始推动时锁定的水平方向

    public bool IsPushing => pushedBox != null;
    public Vector3 PushDirection => pushDirection;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        rb.freezeRotation = true; //冻结刚体旋转，防止角色倾斜
    }

    void Update()
    {
        grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, Ground); //从角色中心点向正下方发射射线

        // 背包打开时屏蔽移动与跳跃输入，并停止跑步动画。
        if (BackpackUIController.IsAnyBackpackOpen)
        {
            EndPush();
            horizontalInput = 0f;
            verticalInput = 0f;
            animator.SetBool("Run", false);
            return;
        }

        MyInput();

        //推动动画的优先级最高。推动过程中无论 W 是否按下，都不允许 Run 抢占。
        if (IsPushing)
        {
            animator.SetBool("Run", false);
            animator.SetBool("Push", true);
        }
        else
        {
            animator.SetBool("Push", false);
            animator.SetBool("Run", horizontalInput != 0f || verticalInput != 0f);
        }
        SpeedControl(); //限制最大速度

        if (grounded) 
            rb.drag = groundDrag; //设置地面阻力，防止角色滑行
        else
            rb.drag = 0; //空中阻力为0，保持空中惯性
    }

    void FixedUpdate() 
    {
        if (BackpackUIController.IsAnyBackpackOpen)
        {
            // 清除水平惯性，但保留竖直速度与重力，避免空中打开背包时悬浮。
            rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
            return;
        }

        MovePlayer(); 
        rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration); //应用自定义重力
    }

    private void MyInput()
    {
        if (IsPushing)
        {
            horizontalInput = 0f; //推动期间先禁用左右移动
            verticalInput = Mathf.Max(0f, Input.GetAxisRaw("Vertical")); //只允许 W 向前推
            return; //推动期间不允许跳跃
        }

        horizontalInput = Input.GetAxisRaw("Horizontal"); //获取水平方向输入
        verticalInput = Input.GetAxisRaw("Vertical"); //获取垂直方向输入

        if (Input.GetKeyDown(jumpKey) && readyToJump && grounded) //同时满足 本帧刚按下跳跃键；跳跃冷却已经结束；角色处于地面。
        {
            readyToJump = false; //跳跃触发后，暂时禁止再次跳跃
            Jump();
            Invoke(nameof(ResetJump), jumpCooldown); //重置跳跃冷却
        }
    }

    private void MovePlayer()
    {
        //推动位移由 PushableBox 统一计算并同时应用给角色和箱子。
        //这里不能再单独 AddForce，否则两者速度不同，距离会越推越远。
        if (IsPushing)
            return;

        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;

        // 有输入：沿用原本的AddForce推力逻辑
        if (moveDirection.magnitude > 0.01f)
        {
            if (grounded)
            {
                rb.AddForce(
                    moveDirection.normalized * moveSpeed * 10f,
                    ForceMode.Force
                ); //地面移动
            }
            else
            {
                rb.AddForce(
                    moveDirection.normalized * moveSpeed * 10f * airMultiplier,
                    ForceMode.Force
                ); //空中移动，乘以空中移动倍率
            }
        }
    }

    //由当前推动的箱子调用，确保角色和箱子使用完全相同的水平位移。
    public void ApplyPushMovement(PushableBox box, Vector3 movementDelta)
    {
        if (box == null || pushedBox != box || rb == null)
            return;

        rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
        rb.MovePosition(rb.position + movementDelta);
    }


    private void SpeedControl() //限制最大速度
    {
        Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z); //获取水平速度分量

        float currentMaxSpeed = IsPushing ? pushMoveSpeed : moveSpeed;
        if (flatVel.magnitude > currentMaxSpeed) //如果水平速度超过当前状态的最大速度
        {
            Vector3 limitedVel = flatVel.normalized * currentMaxSpeed; //限制水平速度为当前最大速度
            rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z); 
        }
    }

    private void Jump()
    {
        animator.SetTrigger("Jump"); //触发跳跃动画
        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z); //重置垂直速度，确保跳跃高度一致
        rb.AddForce(transform.up * jumpForce, ForceMode.Impulse); //施加向上的冲力，触发跳跃
    }

    private void ResetJump() //重置跳跃冷却
    {
        readyToJump = true;
    }

    //玩家进入推箱状态
    public bool BeginPush(PushableBox box, Vector3 direction)
    {
        //箱子不存在或背包正在打开时，不能开始推动。
        if (box == null || BackpackUIController.IsAnyBackpackOpen)
            return false;

        //推箱只使用水平方向，忽略箱子与玩家之间的高度差。
        direction.y = 0f;
        //方向长度太小时无法确定推动方向，取消推动。
        if (direction.sqrMagnitude < 0.01f)
            return false;

        pushedBox = box; //记录当前正在推动的箱子；不为空就代表玩家处于推动状态。
        pushDirection = direction.normalized; //保存单位方向，推动期间角色和箱子都沿这个方向移动。

        //Update 的执行顺序不固定，进入推动的当帧就关闭 Run，避免闪一下跑步动画。
        if (animator != null)
        {
            animator.SetBool("Run", false);
            animator.SetBool("Push", true);
        }
        return true; //通知箱子脚本：玩家已经成功进入推动状态。
    }

    //结束推箱状态。box 留空时强制结束；传入箱子时，只允许当前箱子结束自己的推动状态。
    public void EndPush(PushableBox box = null)
    {
        if (box != null && pushedBox != box)
            return;

        pushedBox = null;
        pushDirection = Vector3.zero; 
        if (animator != null)
            animator.SetBool("Push", false); //退出推箱动画，恢复待机或普通移动动画。
    }

    //玩家物体被关闭或场景切换时，确保不会残留推箱状态和动画参数。
    private void OnDisable()
    {
        EndPush();
    }

}
