using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControllerTypeFinder : MonoBehaviour
{
    public bool isHand;
    public bool isGamificationExist; 
    public bool isQuest3;
    public GamificationManager gamificationManager;
    private void OnEnable()
    {
        GamificationControllerHandSwitcher switcher = GamificationControllerHandSwitcher.Instance;
        if (switcher == null)
        {
            return;
        }

        if (!isHand)
        {
            switcher.isHandMode = false;
            switcher.SwitchControllerandhand(switcher.isHandMode);
            if (isGamificationExist)
            {
                if (ControllerSwitchQuest.Instance != null)
                {
                    ControllerSwitchQuest.Instance.isQuest3 = isQuest3;
                    ControllerSwitchQuest.Instance.AssignGamification();
                }
                Transform getChildObj = this.gameObject.transform.GetChild(0);
                if (gamificationManager != null) gamificationManager.rightcontroller2 = getChildObj.gameObject;
            }
            else
            {
                Transform getChildObj = this.gameObject.transform.GetChild(0);
                if (gamificationManager != null) gamificationManager.rightcontroller1 = getChildObj.gameObject;
            }
        }
        else
        {
            switcher.isHandMode = true;
            switcher.SwitchControllerandhand(switcher.isHandMode);
        }
     
    }

    private void OnDisable()
    {
        GamificationControllerHandSwitcher switcher = GamificationControllerHandSwitcher.Instance;
        if (switcher == null)
        {
            return;
        }

        if (!isHand)
        {
            switcher.isHandMode = true;
            switcher.SwitchControllerandhand(switcher.isHandMode);
        }
        else
        {
            switcher.isHandMode = false;
            switcher.SwitchControllerandhand(switcher.isHandMode);
        }
    }
}
