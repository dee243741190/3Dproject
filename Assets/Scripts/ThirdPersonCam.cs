using System.Collections;
using UnityEngine;
using Cinemachine; //访问 Cinemachine 摄像机系统

public class ThirdPersonCam : MonoBehaviour
{
    public Transform orientation; //角色方向参考
    public Transform playerObj; //角色模型参考

    public float rotationSpeed = 7f; //摄像机旋转速度

    IEnumerator Start()
    {
        CinemachineFreeLook freeLook = FindObjectOfType<CinemachineFreeLook>();

        //目的是让游戏开始时，摄像机方向和角色初始朝向基本一致
        freeLook.m_XAxis.m_InputAxisName = ""; //清空 Cinemachine 的输入轴名称，临时禁止鼠标控制摄像机
        freeLook.m_YAxis.m_InputAxisName = ""; 
        freeLook.m_XAxis.Value = playerObj.eulerAngles.y; //将摄像机的水平旋转值设置为角色模型的当前旋转角度
        freeLook.m_YAxis.Value = 0.5f; //将摄像机的垂直旋转值设置为中间位置（0.5 表示中间）

        Cursor.lockState = CursorLockMode.Locked; //锁定鼠标光标，使其在游戏窗口内不可见并固定在屏幕中心
        Cursor.visible = false; //隐藏鼠标光标

        yield return null; //等待两帧，确保摄像机和角色的初始状态已经正确设置
        yield return null;

        freeLook.m_XAxis.Value = playerObj.eulerAngles.y; //再次设置摄像机角度
        freeLook.m_YAxis.Value = 0.5f;
        freeLook.m_XAxis.m_InputAxisName = "Mouse X"; //恢复 Cinemachine 的输入轴名称，使鼠标可以控制摄像机
        freeLook.m_YAxis.m_InputAxisName = "Mouse Y";
    }

    void Update()
    {
        Vector3 viewDir = Camera.main.transform.forward; //获取摄像机的前向方向
        viewDir.y = 0f;
        orientation.forward = viewDir.normalized; //将角色方向参考的前向方向设置为摄像机的水平前向方向

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