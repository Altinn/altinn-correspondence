﻿namespace Altinn.Correspondence.Application.InitializeServiceOwner;

public class InitializeServiceOwnerRequest
{
    public string ServiceOwnerId { get; set; } = string.Empty;

    public string ServiceOwnerName { get; set; } = string.Empty;
}
