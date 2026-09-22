using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;
public sealed record RaceResult(string RaceId,string PlayerId,string Name,string Track,int Rank,float Time,float BestLap,bool Dnf,DateTime FinishedAt);
public sealed class ResultStore
{
    readonly string? connection;
    readonly string localPath;
    readonly SemaphoreSlim mutex=new(1);
    public ResultStore(IConfiguration config,IWebHostEnvironment env)
    {
        connection=config.GetConnectionString("RacerSql");
        localPath=Path.Combine(env.ContentRootPath,"App_Data","results.json");
        if(string.IsNullOrEmpty(connection)&&!env.IsDevelopment())throw new InvalidOperationException("Production requires ConnectionStrings__RacerSql. Configure Azure SQL free offer.");
    }
    public async Task Save(RaceResult[] rows,CancellationToken ct)
    {
        if(string.IsNullOrEmpty(connection)){
            await mutex.WaitAsync(ct);try{
                Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                var data=File.Exists(localPath)?JsonSerializer.Deserialize<List<RaceResult>>(await File.ReadAllTextAsync(localPath,ct))!:new();
                foreach(var row in rows)if(!data.Any(x=>x.RaceId==row.RaceId&&x.PlayerId==row.PlayerId))data.Add(row);
                await File.WriteAllTextAsync(localPath,JsonSerializer.Serialize(data.TakeLast(5000)),ct);
            }finally{mutex.Release();}return;
        }
        await Retry(async()=>{
            await using var db=new SqlConnection(connection);await db.OpenAsync(ct);
            await using var transaction=(SqlTransaction)await db.BeginTransactionAsync(ct);
            foreach(var row in rows){
                await using var cmd=db.CreateCommand();
                cmd.Transaction=transaction;cmd.CommandText="IF NOT EXISTS (SELECT 1 FROM dbo.RaceResults WITH (UPDLOCK,HOLDLOCK) WHERE RaceId=@race AND PlayerId=@player) INSERT dbo.RaceResults (RaceId,PlayerId,Name,Track,Rank,Time,BestLap,Dnf,FinishedAt) VALUES (@race,@player,@name,@track,@rank,@time,@best,@dnf,@date)";
                cmd.Parameters.Add("@race",SqlDbType.VarChar,32).Value=row.RaceId;cmd.Parameters.Add("@player",SqlDbType.VarChar,32).Value=row.PlayerId;
                cmd.Parameters.Add("@name",SqlDbType.NVarChar,32).Value=row.Name;cmd.Parameters.Add("@track",SqlDbType.VarChar,16).Value=row.Track;
                cmd.Parameters.Add("@rank",SqlDbType.Int).Value=row.Rank;cmd.Parameters.Add("@time",SqlDbType.Real).Value=row.Time;
                cmd.Parameters.Add("@best",SqlDbType.Real).Value=row.BestLap;cmd.Parameters.Add("@dnf",SqlDbType.Bit).Value=row.Dnf;cmd.Parameters.Add("@date",SqlDbType.DateTime2).Value=row.FinishedAt;
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await transaction.CommitAsync(ct);
        },ct);
    }
    public async Task<object> Read(string track,CancellationToken ct)
    {
        if(string.IsNullOrEmpty(connection)){
            await mutex.WaitAsync(ct);try{
                var rows=File.Exists(localPath)?JsonSerializer.Deserialize<List<RaceResult>>(await File.ReadAllTextAsync(localPath,ct))!:new();
                return rows.Where(r=>r.Track==track&&!r.Dnf).OrderBy(r=>r.Time).Take(10).ToArray();
            }finally{mutex.Release();}
        }
        var result=new List<object>();
        await Retry(async()=>{
            result.Clear();await using var db=new SqlConnection(connection);await db.OpenAsync(ct);
            await using var cmd=db.CreateCommand();cmd.CommandText="SELECT TOP (10) Name,Time,BestLap FROM dbo.RaceResults WHERE Track=@track AND Dnf=0 ORDER BY Time";
            cmd.Parameters.Add("@track",SqlDbType.VarChar,16).Value=track;
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))result.Add(new{Name=reader.GetString(0),Time=reader.GetFloat(1),BestLap=reader.GetFloat(2)});
        },ct);return result;
    }
    static async Task Retry(Func<Task> work,CancellationToken ct)
    {
        for(int attempt=0;;attempt++){
            try{await work();return;}catch(SqlException)when(attempt<2){await Task.Delay(TimeSpan.FromSeconds((attempt+1)*2),ct);}
        }
    }
}