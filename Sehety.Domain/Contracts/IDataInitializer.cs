using System;
using System.Collections.Generic;
using System.Text;

namespace S2S.Domain.Contracts
{
	public interface IDataInitializer
	{
		Task InitializeAsync();
	}
}
