// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using UnityEngine;

namespace UnityStandardAssets.ImageEffects
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Image Effects/Other/Screen Overlay")]
    public class ScreenOverlay : PostEffectsBase
    {
        public enum OverlayBlendMode
        {
            Additive = 0,
            ScreenBlend = 1,
            Multiply = 2,
            Overlay = 3,
            AlphaBlend = 4,
        }

        public OverlayBlendMode blendMode = OverlayBlendMode.Overlay;
        public float intensity = 1.0f;
        public Texture2D texture = null;

        public Shader overlayShader = null;
        private Material _overlayMaterial = null;


        public override bool CheckResources()
        {
            CheckSupport(false);

            _overlayMaterial = CheckShaderAndCreateMaterial(overlayShader, _overlayMaterial);

            if (!isSupported)
                ReportAutoDisable();
            return isSupported;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (CheckResources() == false)
            {
                Graphics.Blit(source, destination);
                return;
            }

            Vector4 UV_Transform = new Vector4(1, 0, 0, 1);

#if UNITY_WP8
	    	// WP8 has no OS support for rotating screen with device orientation,
	    	// so we do those transformations ourselves.
			if (Screen.orientation == ScreenOrientation.LandscapeLeft) {
				UV_Transform = new Vector4(0, -1, 1, 0);
			}
			if (Screen.orientation == ScreenOrientation.LandscapeRight) {
				UV_Transform = new Vector4(0, 1, -1, 0);
			}
			if (Screen.orientation == ScreenOrientation.PortraitUpsideDown) {
				UV_Transform = new Vector4(-1, 0, 0, -1);
			}
#endif

            _overlayMaterial.SetVector("_UV_Transform", UV_Transform);
            _overlayMaterial.SetFloat("_Intensity", intensity);
            _overlayMaterial.SetTexture("_Overlay", texture);
            Graphics.Blit(source, destination, _overlayMaterial, (int)blendMode);
        }
    }
}