using System.Collections;
using UnityEngine;
using Cinemachine; //访问 Cinemachine 摄像机系统

public class ThirdPersonCam : MonoBehaviour
{
    public Transform orientation; //角色方向参考
    public Transform playerObj; //角色模型参考

    public float rotationSpeed = 7f; //摄像机旋转速度
    PlayerController playerController;

    void Awake()
    {
        InitializeCamera();
    }

    private bool initialized;
    private CinemachineFreeLook freeLook;
    private string xInputName;
    private string yInputName;

    public void SetMouseInputEnabled(bool allowInput)
    {
        InitializeCamera();
        if (freeLook == null) return;
        freeLook.m_XAxis.Reset();
        freeLook.m_YAxis.Reset();
        freeLook.m_XAxis.m_InputAxisValue = 0f;
        freeLook.m_YAxis.m_InputAxisValue = 0f;
        freeLook.m_XAxis.m_InputAxisName = allowInput ? xInputName : string.Empty;
        freeLook.m_YAxis.m_InputAxisName = allowInput ? yInputName : string.Empty;
    }

    public void InitializeCamera()
    {
        if (initialized) return;
        playerController = GetComponent<PlayerController>();
        freeLook = FindObjectOfType<CinemachineFreeLook>();

        if (freeLook == null)
            return;
        initialized = true;
        xInputName = freeLook.m_XAxis.m_InputAxisName;
        yInputName = freeLook.m_YAxis.m_InputAxisName;

        //目的是让游戏开始时，摄像机方向和角色初始朝向基本一致
        freeLook.m_XAxis.m_InputAxisName = ""; //清空 Cinemachine 的输入轴名称，临时禁止鼠标控制摄像机
        freeLook.m_YAxis.m_InputAxisName = ""; 
        freeLook.m_XAxis.Value = playerObj.eulerAngles.y; //将摄像机的水平旋转值设置为角色模型的当前旋转角度
        freeLook.m_YAxis.Value = 0.5f; //将摄像机的垂直旋转值设置为中间位置（0.5 表示中间）

        // 在启动时完成定位，不等到 Timeline 结束启用组件后再重置。
        freeLook.UpdateCameraState(Vector3.up, -1f);

        freeLook.m_XAxis.m_InputAxisName = xInputName;
        freeLook.m_YAxis.m_InputAxisName = yInputName;
    }

    void Update()
    {
        // 背包打开时不再根据键盘输入旋转角色模型。
        if (BackpackUIController.IsAnyBackpackOpen)
            return;

        if (Camera.main == null)
            return;

        Vector3 viewDir = Camera.main.transform.forward; //获取摄像机的前向方向
        viewDir.y = 0f;
        orientation.forward = viewDir.normalized; //将角色方向参考的前向方向设置为摄像机的水平前向方向

        // 玩家正在推箱子时，不再根据普通移动输入旋转角色模型。
        // 角色会平滑转向开始推动时锁定的方向，避免推动过程中身体转向摄像机或其他方向。
        if (playerController != null && playerController.IsPushing)
        {
            playerObj.forward = Vector3.Slerp(
                playerObj.forward,                    //角色模型当前朝向
                playerController.PushDirection,      //开始推箱时记录的推动方向
                Time.deltaTime * rotationSpeed);      //按旋转速度平滑转向，避免突然转身
            return; //推动状态下不再执行下面的普通移动朝向逻辑
        }

        float horizontalInput = Input.GetAxisRaw("Horizontal"); //获取水平输入
        float verticalInput = Input.GetAxisRaw("Vertical"); //获取垂直输入
        Vector3 inputDir = orientation.forward * verticalInput + orientation.right * horizontalInput; //计算输入方向，基于角色方向参考和玩家输入

        if (inputDir != Vector3.zero) //如果有输入方向，则进行角色模型的旋转，使其朝向输入方向
        {
            playerObj.forward = Vector3.Slerp(
                playerObj.forward,
                inputDir.normalized,
                Time.deltaTime * rotationSpeed
            );
        }
    }

}
