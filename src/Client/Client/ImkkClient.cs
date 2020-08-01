using System;
using System.Diagnostics;

namespace IMKK.Client {
	public class ImkkClient: IDisposable {
		#region data

		private object instanceLocker = new object();

		#endregion


		#region creation & disposal

		public ImkkClient() {
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
