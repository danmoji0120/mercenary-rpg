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
            PruneInactiveJobs();
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

    public int InactiveJobCount
    {
        get
        {
            int count = 0;

            foreach (CraftJob job in _jobs)
            {
                job.PruneReservations();

                if (!IsActiveJob(job))
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
        PruneInactiveJobs();
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

    public IReadOnlyList<CraftJob> GetInactiveJobs()
    {
        List<CraftJob> jobs = new();

        foreach (CraftJob job in _jobs)
        {
            job.PruneReservations();

            if (!IsActiveJob(job))
            {
                jobs.Add(job);
            }
        }

        return jobs;
    }

    public IReadOnlyList<CraftJob> GetMaterialDeliveryJobs()
    {
        PruneInactiveJobs();
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
        PruneInactiveJobs();
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

    public IReadOnlyList<CraftJob> GetOutputReadyJobs()
    {
        PruneInactiveJobs();
        List<CraftJob> jobs = new();

        foreach (CraftJob job in _jobs)
        {
            job.PruneReservations();

            if (IsActiveJob(job)
                && job.State == CraftJobState.OutputReady
                && job.HasProducedOutputs)
            {
                jobs.Add(job);
            }
        }

        return jobs;
    }

    public bool TryFindOutputPickupJob(out CraftJob? job)
    {
        foreach (CraftJob candidate in GetOutputReadyJobs())
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

        PruneInactiveJobs();
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
        PruneInactiveJobs();

        foreach (CraftJob job in _jobs)
        {
            job.PruneReservations();

            if (job.State == CraftJobState.OutputReady
                && job.RequiredOutputs.Count > 0
                && job.ProducedOutputs.Count <= 0)
            {
                job.TryFinalizeOutputs();
            }
        }

        PruneInactiveJobs();
    }

    public int PruneInactiveJobs()
    {
        int removedCount = 0;

        for (int index = _jobs.Count - 1; index >= 0; index--)
        {
            CraftJob job = _jobs[index];
            job.PruneReservations();

            if (IsActiveJob(job))
            {
                continue;
            }

            _jobs.RemoveAt(index);
            removedCount++;
        }

        return removedCount;
    }

    private static bool IsActiveJob(CraftJob job)
    {
        return !job.IsCompleted && !job.IsCancelled;
    }
}
