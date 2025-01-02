using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DB {
	// Funciona como override de System.Data.IsolationLevel,
	// para evitar que el usuario tenga que usar este enum en DBController,
	// y para evitar que se usen ciertos isolation levels poco estrictos
	public enum IsolationLevel {
		ReadCommitted = 4096, 
		RepeatableRead = 65536, 
		Serializable = 1048576, 
		Snapshot = 16777216
	}
}
