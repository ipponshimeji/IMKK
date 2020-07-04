using System;
using System.Diagnostics;

namespace IMKK.Client {
	public class IMMKClient: IDisposable {
		#region data

		private object instanceLocker = new object();

		#endregion


		#region creation & disposal

		public IMMKClient() {
		}

		public virtual void Dispose() {
		}

		#endregion


		#region methods
		#endregion


		#region privates
		#endregion
	}
}
