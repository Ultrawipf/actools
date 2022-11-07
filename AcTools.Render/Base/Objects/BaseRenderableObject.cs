﻿using System;
using System.Collections.Generic;
using AcTools.Render.Base.Cameras;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Base.Objects {
    public abstract class BaseRenderableObject : IRenderableObject {
        public string Name { get; }

        protected BaseRenderableObject([CanBeNull] string name) {
            Name = name;
        }

        public bool IsInitialized { get; private set; }
        public virtual Matrix ParentMatrix { get; set; }
        public virtual bool IsReflectable { get; set; } = true;
        public virtual bool IsEnabled { get; set; } = true;
        public abstract int GetTrianglesCount();
        public abstract IEnumerable<int> GetMaterialIds();

        public int GetObjectsCount() {
            return 1;
        }

        public BoundingBox? BoundingBox { get; protected set; }

        public void Draw(IDeviceContextHolder holder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            if (filter?.Invoke(this) == false) return;
            if (!IsEnabled || mode == SpecialRenderMode.Reflection && !IsReflectable) return;

            if (!IsInitialized) {
                Initialize(holder);
                IsInitialized = true;
            }

            if (mode != SpecialRenderMode.InitializeOnly) {
                DrawOverride(holder, camera, mode);
            }
        }

        public abstract void UpdateBoundingBox();

        protected abstract void Initialize(IDeviceContextHolder contextHolder);

        protected abstract void DrawOverride(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode);

        public abstract BaseRenderableObject Clone();

        public abstract float? CheckIntersection(Ray ray);

        IRenderableObject IRenderableObject.Clone() {
            return Clone();
        }

        public abstract void Dispose();
    }
}
