﻿using UnityEngine;
using UnityEngine.Serialization;

namespace DapperTest
{
    public class AerialCamera : MonoBehaviour
    {
        private const string HorizontalAxisName = "Horizontal";
        private const string VerticalAxisName = "Vertical";
        private const string ScrollWheelAxisName = "Mouse ScrollWheel";

        [Header("References")]
        [SerializeField] private new Camera camera;
        
        [Header("Pan Settings")]
        [SerializeField] private float regularPanSpeed;
        [SerializeField] private float fastPanSpeed;

        [Header("Zoom Settings")]
        [SerializeField] private float minSize;
        [SerializeField] private float maxSize;
        [SerializeField] private float zoomSpeed;

        private Vector3 initialPosition;
        private float initialZoom;

        private void Awake()
        {
            StoreDefaultValues();

            camera.orthographicSize = maxSize;
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Home) || Input.GetKeyDown(KeyCode.R))
            {
                ResetToDefaultValues();
                return;
            }
            
            HandlePan();
            HandleZoom();
        }
        
        private void StoreDefaultValues()
        {
            initialPosition = transform.position;
        }

        private void ResetToDefaultValues()
        {
            transform.position = initialPosition;
            camera.orthographicSize = maxSize;
        }

        private void HandlePan()
        {
            float horizontalPanInput = Input.GetAxisRaw(HorizontalAxisName);
            float verticalPanInput = Input.GetAxisRaw(VerticalAxisName);

            Transform transform = this.transform;
            Vector3 localHorizontalPanInput = transform.right * horizontalPanInput;
            Vector3 localVerticalPanInput = transform.up * verticalPanInput;

            Vector3 localPanInput = localHorizontalPanInput + localVerticalPanInput;
            localPanInput.y = 0f;

            localPanInput.Normalize();

            bool anyShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float panSpeed = anyShiftHeld ? fastPanSpeed : regularPanSpeed;

            Vector3 displacement = localPanInput * (panSpeed * Time.deltaTime);

            transform.position += displacement;
        }

        private void HandleZoom()
        {
            float zoomInput = Input.GetAxisRaw(ScrollWheelAxisName);
            
            // flip zoom input because Camera's orthographicSize works the
            // opposite way
            zoomInput = -zoomInput;

            float newOrthoSize = camera.orthographicSize + zoomInput * zoomSpeed * Time.deltaTime;
            newOrthoSize = Mathf.Clamp(newOrthoSize, minSize, maxSize);

            camera.orthographicSize = newOrthoSize;
        }
    }
}
