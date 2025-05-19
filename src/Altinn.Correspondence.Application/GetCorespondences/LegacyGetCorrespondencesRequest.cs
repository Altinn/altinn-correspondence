using Altinn.Correspondence.Core.Models.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.Application.GetCorrespondences;

public class LegacyGetCorrespondencesRequest
{
    public required int[] InstanceOwnerPartyIdList { get; set; }

    public bool IncludeActive { get; set; }

    public bool IncludeArchived { get; set; }

    public bool IncludeDeleted { get; set; }

    public bool FilterMigrated { get; set; } = true;

    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }

    public string? SearchString { get; set; }

    public string? Language { get; set; }

    public CorrespondenceStatus? Status { get; set; }
}
