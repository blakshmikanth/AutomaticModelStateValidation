﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace AutomaticModelStateValidation
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class AutoValidateModelAttribute : ActionFilterAttribute
    {
        private const string ActionNameKey = "action";

        private readonly string controller;
        private readonly string action;

        public AutoValidateModelAttribute(string action)
        {
            this.action = action;
        }

        public AutoValidateModelAttribute(string controller, string action)
        {
            this.controller = controller;
            this.action = action;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            string SansController(string controllerName)
            {
                if (controllerName.EndsWith("Controller"))
                {
                    return controllerName.Substring(0, controllerName.Length - 10);
                }

                return controllerName;
            }

            if (!context.ModelState.IsValid)
            {
                var modelState = context.ModelState;
                var services = context.HttpContext.RequestServices;
                var controllerName = SansController(controller ?? context.Controller.GetType().Name);

                var actionDescriptorCollectionProvider =
                    (IActionDescriptorCollectionProvider) services.GetService(
                        typeof(IActionDescriptorCollectionProvider));

                var controllerActionDescriptors =
                    actionDescriptorCollectionProvider
                    .ActionDescriptors.Items
                    .OfType<ControllerActionDescriptor>();

                var controllerActionDescriptor =
                    controllerActionDescriptors
                    .Where(x => x.ControllerName == controllerName && x.ActionName == action)
                    .FirstOrDefault();

                if (controllerActionDescriptor == null)
                {
                    throw new FailedToFindActionException(controllerName, action);
                }

                var actionContext =
                    new ActionContext(context.HttpContext, context.RouteData, controllerActionDescriptor, modelState);

                var actionInvokerFactory = (IActionInvokerFactory) services.GetService(typeof(IActionInvokerFactory));
                var actionContextAccessor = (IActionContextAccessor) services.GetService(typeof(IActionContextAccessor));

                if (actionContextAccessor != null)
                {
                    actionContextAccessor.ActionContext = actionContext;
                }

                if (context.RouteData.Values.ContainsKey(ActionNameKey))
                {
                    context.RouteData.Values[ActionNameKey] = controllerActionDescriptor.ActionName;
                }

                var invoker = actionInvokerFactory.CreateInvoker(actionContext);
                await invoker.InvokeAsync();
            }
            else
            {
                await next();
            }
        }

    }

    public class FailedToFindActionException : Exception
    {
        public FailedToFindActionException(string controller, string action)
            : base($"Failed to find the action for controller named '{controller}' and action named '{action}'")
        {
        }
    }
}