﻿using System;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using Debug = System.Diagnostics.Debug;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class AmbientShadowMaterialSimple : IAmbientShadowMaterial {
        private readonly Kn5AmbientShadowMaterialDescription _description;
        private EffectDarkMaterial _effect;

        private IRenderableTexture _txDiffuse;

        internal AmbientShadowMaterialSimple([NotNull] Kn5AmbientShadowMaterialDescription description) {
            _description = description ?? throw new ArgumentNullException(nameof(description));
        }

        public void EnsureInitialized(IDeviceContextHolder contextHolder) {
            if (_effect != null) return;
            _effect = contextHolder.GetEffect<EffectDarkMaterial>();
            _txDiffuse = contextHolder.Get<ITexturesProvider>().GetTexture(contextHolder, _description.Filename);
        }

        public void Refresh(IDeviceContextHolder contextHolder) {}

        private RasterizerState _rasterizerState;

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Simple && mode != SpecialRenderMode.Outline) return false;

            _effect.FxDiffuseMap.SetResource(_txDiffuse);
            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutPT;

            if (mode != SpecialRenderMode.Outline) {
                contextHolder.DeviceContext.OutputMerger.BlendState = contextHolder.States.TransparentBlendState;
                contextHolder.DeviceContext.OutputMerger.DepthStencilState = contextHolder.States.ReadOnlyDepthState;

                _rasterizerState = contextHolder.DeviceContext.Rasterizer.State;
                if (_rasterizerState == null) {
                    contextHolder.DeviceContext.Rasterizer.State = contextHolder.States.AmbientShadowState;
                }
            }

            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
            _effect.FxWorld.SetMatrix(objectTransform);
        }

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            _effect.TechAmbientShadow.DrawAllPasses(contextHolder.DeviceContext, indices);
            contextHolder.DeviceContext.OutputMerger.BlendState = null;
            contextHolder.DeviceContext.OutputMerger.DepthStencilState = null;
            contextHolder.DeviceContext.Rasterizer.State = _rasterizerState;
        }

        public bool IsBlending => false;

        public string Name => null;

        public void Dispose() {
            Debug.WriteLine("AMBIENT SHADOW MATERIAL DISPOSED");
            DisposeHelper.Dispose(ref _txDiffuse);
        }

        public ShaderResourceView GetView(IDeviceContextHolder contextHolder) {
            if (_txDiffuse == null) {
                EnsureInitialized(contextHolder);
            }

            return _txDiffuse?.Resource;
        }
    }
}
