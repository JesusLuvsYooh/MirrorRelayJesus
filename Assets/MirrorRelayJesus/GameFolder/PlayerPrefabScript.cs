using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class PlayerPrefabScript : NetworkBehaviour
{
    public float moveSpeed = 5f;

    private CharacterController controller;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (isLocalPlayer == false) return;


        // Get input (WASD, Arrow Keys, Controller, etc.)
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // Build movement vector (top-down = X/Z plane)
        Vector3 movement = new Vector3(horizontal, 0f, vertical);

        // Normalize so diagonals aren't faster
        if (movement.magnitude > 1f)
            movement.Normalize();

        // Move character
        controller.Move(movement * moveSpeed * Time.deltaTime);
    }
}
