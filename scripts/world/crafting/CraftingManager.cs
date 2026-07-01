using System.Collections.Generic;
using Godot;

public partial class CraftingManager : Node
{
    private readonly List<CraftJob> _jobs = new();
    private int _nextJobId = 1;

    public int ActiveJobCount
    {
        get
        {
            int count = 0;

            foreach (CraftJob job in _jobs)
            {
                if (IsActiveJob(job))
                {
                    count++;
                }
            }

            return count;
        }
    }

    public IReadOnlyList<CraftJob> GetAllJobs()
    {
        return _jobs;
    }

    public IReadOnlyList<CraftJob> GetActiveJobs()
    {
        List<CraftJob> jobs = new();

        foreach (CraftJob job in _jobs)
        {
            if (IsActiveJob(job))
            {
                jobs.Add(job);
            }
        }

        return jobs;
    }

    public IReadOnlyList<CraftJob> GetMaterialDeliveryJobs()
    {
        List<CraftJob> jobs = new();

        foreach (CraftJob job in _jobs)
        {
            job.PruneReservations();

            if (IsActiveJob(job)
                && job.State == CraftJobState.WaitingForMaterials
                && !job.HasAllMaterials
                && job.GetFirstMissingInput().HasValue)
            {
                jobs.Add(job);
            }
        }

        return jobs;
    }

    public bool TryFindMaterialDeliveryJob(out CraftJob? job)
    {
        foreach (CraftJob candidate in GetMaterialDeliveryJobs())
        {
            job = candidate;
            return true;
        }

        job = null;
        return false;
    }

    public IReadOnlyList<CraftJob> GetReadyToCraftJobs()
    {
        List<CraftJob> jobs = new();

        foreach (CraftJob job in _jobs)
        {
            job.PruneReservations();

            if (IsActiveJob(job)
                && job.State == CraftJobState.ReadyToCraft
                && job.HasAllMaterials)
            {
                jobs.Add(job);
            }
        }

        return jobs;
    }

    public bool TryFindReadyToCraftJob(out CraftJob? job)
    {
        foreach (CraftJob candidate in GetReadyToCraftJobs())
        {
            job = candidate;
            return true;
        }

        job = null;
        return false;
    }

    public bool TryGetRecipeForJob(CraftJob job, out CraftRecipeEntry recipe)
    {
        recipe = default!;
        return job != null && CraftRecipeDatabase.TryGet(job.RecipeId, out recipe);
    }

    public bool TryCreateJob(string recipeId, Vector2I facilityCell, out CraftJob? job)
    {
        job = null;

        if (!CraftRecipeDatabase.TryGet(recipeId, out CraftRecipeEntry recipe) || !recipe.IsEnabled)
        {
            return false;
        }

        if (TryGetJobAtFacility(facilityCell, out _))
        {
            return false;
        }

        CraftJob createdJob = new()
        {
            JobId = _nextJobId++,
            RecipeId = recipe.RecipeId,
            FacilityCell = facilityCell,
            RequiredWork = recipe.RequiredWork
        };
        createdJob.SetRequirements(recipe);

        _jobs.Add(createdJob);
        job = createdJob;
        return true;
    }

    public bool TryCreateDebugWoodPlankJob(Vector2I facilityCell, out CraftJob? job)
    {
        return TryCreateJob("process_wood_plank", facilityCell, out job);
    }

    public bool CancelJob(CraftJob job)
    {
        if (job == null || job.IsCompleted || job.IsCancelled)
        {
            return false;
        }

        job.Cancel();
        return true;
    }

    public int CancelAllDebugJobs()
    {
        int cancelledCount = 0;

        foreach (CraftJob job in _jobs)
        {
            if (IsActiveJob(job))
            {
                job.Cancel();
                cancelledCount++;
            }
        }

        return cancelledCount;
    }

    public bool TryGetJobAtFacility(Vector2I facilityCell, out CraftJob? job)
    {
        foreach (CraftJob candidate in _jobs)
        {
            if (candidate.FacilityCell == facilityCell && IsActiveJob(candidate))
            {
                job = candidate;
                return true;
            }
        }

        job = null;
        return false;
    }

    public bool TryGetJobById(int jobId, out CraftJob? job)
    {
        foreach (CraftJob candidate in _jobs)
        {
            if (candidate.JobId == jobId)
            {
                job = candidate;
                return true;
            }
        }

        job = null;
        return false;
    }

    public IReadOnlyList<CraftJob> GetJobsForRecipe(string recipeId)
    {
        List<CraftJob> jobs = new();

        foreach (CraftJob job in _jobs)
        {
            if (job.RecipeId == recipeId)
            {
                jobs.Add(job);
            }
        }

        return jobs;
    }

    public void ValidateJobs()
    {
        foreach (CraftJob job in _jobs)
        {
            job.PruneReservations();
        }
    }

    private static bool IsActiveJob(CraftJob job)
    {
        return !job.IsCompleted && !job.IsCancelled;
    }
}
