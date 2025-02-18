using System;
using System.Collections.Generic;
using System.Collections.Generic;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    private int minutes;
    public int Minutes {get { return minutes; } set { minutes = value; } }
    
    private int hours;
    public int Hours {get { return hours; } set { hours = value; } }
    
    private int days;
    
    private float tempSecond;
    public void Update()
    {
        tempSecond += Time.deltaTime;
        if (tempSecond >= 1)
        {
            minutes += 1;
            tempSecond = 0;
        }
    }
}
