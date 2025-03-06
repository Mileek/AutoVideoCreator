using Autofac;
using Caliburn.Micro;
using System.ComponentModel;
using System.Linq;

namespace AutoVideoCreator.Application
{
    class Module: Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            RegisterViews(builder);
            RegisterViewModels(builder);
            RegisterTypes(builder);
        }

        private void RegisterTypes(ContainerBuilder builder)
        {
            //  register the single window manager for this container
            builder.RegisterType<WindowManager>().As<IWindowManager>().SingleInstance();
            //  register the single event aggregator for this container
            builder.RegisterType<EventAggregator>().As<IEventAggregator>().SingleInstance();
        }

        private void RegisterViews(ContainerBuilder builder)
        {
            //  register views
            builder.RegisterAssemblyTypes(AssemblySource.Instance.ToArray())
                //  must be a type that ends with View
                .Where(type => type.Name.EndsWith("View"))
                //  registered as self
                .AsSelf()
                //  always create a new one
                .InstancePerDependency();
        }

        private void RegisterViewModels(ContainerBuilder builder)
        {
            //  register view models
            builder.RegisterAssemblyTypes(AssemblySource.Instance.ToArray())
                //  must be a type that ends with ViewModel
                .Where(type => type.Name.EndsWith("ViewModel"))
                //  must implement INotifyPropertyChanged (deriving from PropertyChangedBase will satisfy this)
                .Where(type => type.GetInterface(typeof(INotifyPropertyChanged).Name) != null &&
                               type.GetInterface($"I{type.Name}") != null)
                //  registered as interface(could be changed to self)
                .As(type => type.GetInterface($"I{type.Name}"))
                //  always create a new one
                .InstancePerDependency();
        }
    }
}
