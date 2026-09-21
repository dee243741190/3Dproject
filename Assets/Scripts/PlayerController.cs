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

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        rb.freezeRotation = true; //冻结刚体旋转，防止角色倾斜
    }

void Update()
    {
        grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.2f, Ground); //从角色中心点向正下方发射射线

        MyInput(); 
        animator.SetBool("Run", horizontalInput != 0f || verticalInput != 0f); //设置动画参数，判断是否在移动
        SpeedControl(); //限制最大速度

        if (grounded) 
            rb.drag = groundDrag; //设置地面阻力，防止角色滑行
        else
            rb.drag = 0; //空中阻力为0，保持空中惯性
    }

    void FixedUpdate() 
    {
        MovePlayer(); 
        rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration); //应用自定义重力
    }

    private void MyInput()
    {
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
        moveDirection = orientation.forward * verticalInput
                      + orientation.right * horizontalInput; //计算移动方向，基于角色朝向和输入

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


    private void SpeedControl() //限制最大速度
    {
        Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z); //获取水平速度分量

        if (flatVel.magnitude > moveSpeed) //如果水平速度超过最大移动速度
        {
            Vector3 limitedVel = flatVel.normalized * moveSpeed; //限制水平速度为最大移动速度
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

}
