using AetherCore.Service;
using AetherCore.Utility.Lincense;
using Microsoft.AspNetCore.Mvc;

namespace AetherCore.Controller
{
    [Flags]
    public enum CrudOperation
    {
        None    = 0,
        Create  = 1 << 0,
        Read    = 1 << 1,
        ReadAll = 1 << 2,
        Update  = 1 << 3,
        Delete  = 1 << 4,

        All     = Create | Read | ReadAll | Update | Delete
    }

    // 泛型 Controller，用來抽象出基本的 CRUD 操作邏輯
    public abstract class GenericController<TRequest, TResponse, TService>
        : ControllerBase where TService : IService<TRequest, TResponse>
    {
        // 注入對應的 Service 實作
        protected readonly TService _service;
        private readonly CrudOperation _enabledOperations;

        public GenericController(TService service, CrudOperation enabledOperations = CrudOperation.All)
        {
            _service            = service;
            _enabledOperations  = enabledOperations;
        }

        // C - Create：建立新資料
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TRequest requestDto)
        {
            if (!IsEnabled(CrudOperation.Create))
                return ApiDisabled(nameof(Create));

            return Ok(await OnCreate(requestDto));
        }

        // R - Read All：取得所有資料
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            if (!IsEnabled(CrudOperation.ReadAll))
                return ApiDisabled(nameof(GetAll));

            return Ok(await OnGetAll());
        }

        // R - Read by Id：透過主鍵取得單筆資料
        [HttpGet("{key}")]
        public async Task<IActionResult> Get(string key)
        {
            if (!IsEnabled(CrudOperation.Read))
                return ApiDisabled(nameof(Get));

            return Ok(await OnGet(key));
        }

        // U - Update：更新指定主鍵的資料
        [HttpPut("{key}")]
        public async Task<IActionResult> Update(string key, [FromBody] TRequest requestDto)
        {
            if (!IsEnabled(CrudOperation.Update))
                return ApiDisabled(nameof(Update));

            await XPlanLicenseRuntime.EnsureValidOrThrowAsync();
            await OnUpdate(key, requestDto);

            return Ok();
        }

        // D - Delete：刪除指定主鍵的資料
        [HttpDelete("{key}")]
        public async Task<IActionResult> Delete(string key)
        {
            if (!IsEnabled(CrudOperation.Delete))
                return ApiDisabled(nameof(Delete));

            await OnDelete(key);

            return Ok();
        }

        /*======================================
         * 提供衍生類別覆寫
         * ===================================*/
        // 實際處理建立邏輯，可被覆寫
        protected virtual async Task<TResponse> OnCreate(TRequest request)
        {
            return await _service.CreateAsync(request);
        }

        // 實際處理查詢單筆邏輯，可被覆寫
        protected virtual async Task<TResponse> OnGet(string key)
        {
            return await _service.GetAsync(key);
        }

        // 實際處理查詢全部資料邏輯，可被覆寫
        protected virtual async Task<List<TResponse>> OnGetAll()
        {
            return await _service.GetAllAsync();
        }

        // 實際處理更新邏輯，可被覆寫
        protected virtual async Task OnUpdate(string key, TRequest request)
        {
            await _service.UpdateAsync(key, request);
        }

        // 實際處理刪除邏輯，可被覆寫
        protected virtual async Task OnDelete(string key)
        {
            await _service.DeleteAsync(key);
        }

        /*======================================
         * API開關Helper 
         * ===================================*/
        private bool IsEnabled(CrudOperation operation)
        {
            return (_enabledOperations & operation) == operation;
        }

        protected IActionResult ApiDisabled(string actionName)
        { 
            return NotFound(new
               {
                   message = $"API '{actionName}' is disabled."
               });
        }
    }
}
