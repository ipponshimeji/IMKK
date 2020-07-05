using System;


namespace IMKK.Communication {
	public enum NegotiateStatus {
		Undefined = -1,			// not set
		Succeeded = 0,			// the negotiation succeeded
		Error = 1,				// general error
		InvalidKey = 2,			// the specified key is invalid
		TooManyConnection = 3,	// too many connection 
	}
}
