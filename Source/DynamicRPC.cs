using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WampSharp.Rpc.Server;

namespace Posh
{
	public class CallInvokedArgs
	{
		public CallInvokedArgs(string rpcID)
		{
			RpcID = rpcID;
		}
		
		public string RpcID
		{
			get;
			private set;
		}
	}
	
	/// <summary>
	/// An implementation of <see cref="IWampRpcMetadata"/> and <see cref="IWampRpcMethod"/> in order to allow registration of dynamic RPCs.
	/// </summary>
	public class DynamicRPC: IWampRpcMetadata, IWampRpcMethod
	{
		private string mRPCID;
		private string mUri;
		private Func<object[], object> mMethod;
		private Type[] mParameterTypes;
		public static event EventHandler<CallInvokedArgs> CallInvoked;
		
		private void OnCallInvoked()
		{
			var callInvoked = CallInvoked;
			if(callInvoked != null)
			{
				CallInvoked(this, new CallInvokedArgs(mRPCID));
			}
		}
		
		public DynamicRPC(string rpcID)
		{
			mRPCID = rpcID;
		}
		
		private void DefaultCallInvoked(string rpcID){}
		
		public IEnumerable<IWampRpcMethod> GetServiceMethods()
		{
			yield return this;
		}
		
		public void SetAction(Action action)
		{
			mMethod = (array) => {
				action();
				return null;
			};
			mParameterTypes = new Type[0];
		}
		
		public void SetAction<T1>(Action<T1> action)
		{
			mMethod = (array) => {
				action((T1)array[0]);
				return null;
			};
			mParameterTypes = new Type[]{typeof(T1)};
		}

		public void SetAction<T1, T2>(Action<T1, T2> action)
		{
			mMethod = (array) => {
				action((T1)array[0], (T2)array[1]);
				return null;
			};
			mParameterTypes = new Type[]{typeof(T1), typeof(T2)};
		}
		
		public void SetAction<T1, T2, T3>(Action<T1, T2, T3> action)
		{
			mMethod = (array) => {
				action((T1)array[0], (T2)array[1], (T3)array[2]);
				return null;
			};
			mParameterTypes = new Type[]{typeof(T1), typeof(T2), typeof(T3)};
		}
		
		public void SetAction<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action)
		{
			mMethod = (array) => {
				action((T1)array[0], (T2)array[1], (T3)array[2], (T4)array[3]);
				return null;
			};
			mParameterTypes = new Type[]{typeof(T1), typeof(T2), typeof(T3), typeof(T4)};
		}
		
		public void SetAction<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action)
		{
			mMethod = (array) => {
				action((T1)array[0], (T2)array[1], (T3)array[2], (T4)array[3], (T5)array[4]);
				return null;
			};
			mParameterTypes = new Type[]{typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)};
		}
		
		public void SetAction<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> action)
		{
			mMethod = (array) => {
				action((T1)array[0], (T2)array[1], (T3)array[2], (T4)array[3], (T5)array[4], (T6)array[5]);
				return null;
			};
			mParameterTypes = new Type[]{typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6)};
		}
		
		public void SetAction<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> action)
		{
			mMethod = (array) => {
				action((T1)array[0], (T2)array[1], (T3)array[2], (T4)array[3], (T5)array[4], (T6)array[5], (T7)array[6]);
				return null;
			};
			mParameterTypes = new Type[]{typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7)};
		}
		
		public void SetAction<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> action)
		{
			mMethod = (array) => {
				action((T1)array[0], (T2)array[1], (T3)array[2], (T4)array[3], (T5)array[4], (T6)array[5], (T7)array[6], (T8)array[7]);
				return null;
			};
			mParameterTypes = new Type[]{typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7), typeof(T8)};
		}
		
		public string Name
		{
			get {return mRPCID;}
		}
		
		public string ProcUri
		{
			get {return mRPCID;}
		}
		
		public Type[] Parameters
		{
			get {return mParameterTypes;}
		}
		
		public Task<object> InvokeAsync(object[] parameters)
		{
            var tcs = new TaskCompletionSource<object>();
            tcs.SetResult(Invoke(parameters));
			return tcs.Task;
		}
		
		public object Invoke(object[] parameters)
		{
			var result = mMethod(parameters);
			OnCallInvoked();
			return result;
		}
	}
}