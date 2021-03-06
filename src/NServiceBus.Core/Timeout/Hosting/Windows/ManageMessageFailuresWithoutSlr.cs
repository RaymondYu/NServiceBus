namespace NServiceBus.Timeout.Hosting.Windows
{
    using System;
    using Faults;
    using Logging;
    using Transports;
    using Unicast.Queuing;

    internal class ManageMessageFailuresWithoutSlr : IManageMessageFailures
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(ManageMessageFailuresWithoutSlr));

        private Address localAddress;
        private readonly Address errorQueue;

        public ManageMessageFailuresWithoutSlr(IManageMessageFailures mainFailureManager)
        {
            var mainTransportFailureManager = mainFailureManager as Faults.Forwarder.FaultManager;
            if (mainTransportFailureManager != null)
            {
                errorQueue = mainTransportFailureManager.ErrorQueue;
            }
        }

        public void SerializationFailedForMessage(TransportMessage message, Exception e)
        {
            SendFailureMessage(message, e, "SerializationFailed");
        }

        public void ProcessingAlwaysFailsForMessage(TransportMessage message, Exception e)
        {
            SendFailureMessage(message, e, "ProcessingFailed"); 
        }

        void SendFailureMessage(TransportMessage message, Exception e, string reason)
        {
            if (errorQueue == null)
            {
                Logger.Error("Message processing always fails for message with ID " + message.Id + ".", e);
                return;
            }

            SetExceptionHeaders(message, e, reason);
            try
            {
                var sender = Configure.Instance.Builder.Build<ISendMessages>();

                sender.Send(message, errorQueue);
            }
            catch (Exception exception)
            {
                var queueNotFoundException = exception as QueueNotFoundException;
                string errorMessage;

                if (queueNotFoundException != null)
                {
                    errorMessage = string.Format("Could not forward failed message to error queue '{0}' as it could not be found.", queueNotFoundException.Queue);
                    Logger.Fatal(errorMessage);
                }
                else
                {
                    errorMessage = "Could not forward failed message to error queue.";
                    Logger.Fatal(errorMessage, exception);
                }

                throw new InvalidOperationException(errorMessage, exception);
            }
        }

        public void Init(Address address)
        {
            localAddress = address;
        }

        void SetExceptionHeaders(TransportMessage message, Exception e, string reason)
        {
            message.Headers["NServiceBus.ExceptionInfo.Reason"] = reason;
            message.Headers["NServiceBus.ExceptionInfo.ExceptionType"] = e.GetType().FullName;

            if (e.InnerException != null)
                message.Headers["NServiceBus.ExceptionInfo.InnerExceptionType"] = e.InnerException.GetType().FullName;

            message.Headers["NServiceBus.ExceptionInfo.HelpLink"] = e.HelpLink;
            message.Headers["NServiceBus.ExceptionInfo.Message"] = e.Message;
            message.Headers["NServiceBus.ExceptionInfo.Source"] = e.Source;
            message.Headers["NServiceBus.ExceptionInfo.StackTrace"] = e.StackTrace;

            var failedQ = localAddress ?? Address.Local;

            message.Headers[FaultsHeaderKeys.FailedQ] = failedQ.ToString();
            message.Headers["NServiceBus.TimeOfFailure"] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);
        }
    }
}