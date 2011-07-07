// Type: System.ComponentModel.Component
// Assembly: System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089
// Assembly location: C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0\System.dll

using System;
using System.Runtime;
using System.Runtime.InteropServices;

namespace System.ComponentModel
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDispatch)]
    [DesignerCategory("Component")]
    public class Component : MarshalByRefObject, IComponent, IDisposable
    {
        [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
        public Component();

        protected virtual bool CanRaiseEvents { get; }
        protected EventHandlerList Events { get; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IContainer Container { get; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        protected bool DesignMode { get; }

        #region IComponent Members

        public void Dispose();

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public virtual ISite Site { [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
        get; [TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
        set; }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [Browsable(false)]
        public event EventHandler Disposed;

        #endregion

        ~Component();
        protected virtual void Dispose(bool disposing);
        protected virtual object GetService(Type service);
        public override string ToString();
    }
}
