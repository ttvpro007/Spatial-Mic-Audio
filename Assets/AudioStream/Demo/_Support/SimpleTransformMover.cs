// (c) 2022-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using UnityEngine;

/// <summary>
/// Basic demo script to move a transform in horizontal plane
/// </summary>
public class SimpleTransformMover : MonoBehaviour
{
    /// <summary>
    /// movement offset from input
    /// </summary>
    Vector3 pOffset;
    /// <summary>
    /// 
    /// </summary>
    [SerializeField]
    [Range(1, 20)]
    float speed = 10f;
    Vector3 initialPosition;
    void Awake()
    {
        this.initialPosition = this.transform.position;
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            this.transform.position = this.initialPosition;
        }
        else
        {
            this.pOffset = new Vector3
                (
                    Input.GetAxis("Horizontal") * Time.deltaTime
                    , 0
                    , Input.GetAxis("Vertical") * Time.deltaTime
                )
                * this.speed
                ;

            this.transform.position += this.pOffset;
        }
    }
}
