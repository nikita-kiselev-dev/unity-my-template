﻿using System;
using System.Linq;
using Core.Initialization.Scripts;
using UnityEngine;
using UnityEngine.UI;

namespace Core.View.Canvas.Scripts
{
    public class PopupCanvas : UnityEngine.MonoBehaviour, IPopupCanvas
    {
        [SerializeField] private UnityEngine.Canvas m_Canvas;
        [SerializeField] private Button m_BackgroundButton;
        [SerializeField] private Image m_BackgroundImage;
        
        private Action _onBackgroundClicked;
        
        public Transform ViewParentTransform => gameObject.transform;
        public Image BackgroundImage => m_BackgroundImage;

        public void Init(Action onBackgroundClicked)
        {
            _onBackgroundClicked = onBackgroundClicked;
            SetCamera(GameInfo.MainCameraKey);
            m_BackgroundButton.onClick.RemoveListener(OnBackgroundClick);
            m_BackgroundButton.onClick.AddListener(OnBackgroundClick);
        }

        private void OnBackgroundClick()
        {
            _onBackgroundClicked?.Invoke();
        }
        
        private void SetCamera(string cameraName)
        {
            var mainCamera = GameObject.FindGameObjectsWithTag(cameraName).First().GetComponent<Camera>();
            m_Canvas.worldCamera = mainCamera;
        }
    }
}