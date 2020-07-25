using System;
using System.Diagnostics;

namespace IMKK.Client {
	public class IMKKClient: IDisposable {
		#region data

		private object instanceLocker = new object();

		#endregion


		#region creation & disposal

		public IMKKClient() {
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
