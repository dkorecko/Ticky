using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Ticky.Web.Components.Elements.Sortable
{
    public partial class SortableList<T>
    {
        private DotNetObjectReference<SortableList<T>>? selfReference;

        [Parameter]
        public int Animation { get; set; } = 200;

        [Parameter]
        public string? Filter { get; set; }

        [Parameter]
        public bool ForceFallback { get; set; } = true;

        [Parameter]
        public string Group { get; set; } = Guid.NewGuid().ToString();

        [Parameter]
        public string? Handle { get; set; }

        [Parameter]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Parameter]
        [AllowNull]
        public IList<T> Items { get; set; }

        [Parameter]
        public EventCallback<(
            string movedItemId,
            string? targetItemId,
            string oldColumnId,
            string newColumnId,
            double x,
            double y
        )> OnRemove { get; set; }

        [Parameter]
        public EventCallback<(
            string movedItemId,
            string? targetItemId,
            string columnId
        )> OnUpdate { get; set; }

        [Parameter]
        public string? Pull { get; set; }

        [Parameter]
        public bool Put { get; set; } = true;

        [Parameter]
        public bool Sort { get; set; } = true;

        [Parameter]
        public string? Class { get; set; }

        [Parameter]
        public string? Style { get; set; }

        [Parameter]
        public string Direction { get; set; } = "vertical";

        [Parameter]
        public RenderFragment<T>? SortableItemTemplate { get; set; }

        public void Dispose() => selfReference?.Dispose();

        [JSInvokable]
        public async Task OnRemoveJS(
            string movedItemId,
            string? targetItemId,
            string fromId,
            string toId,
            double x,
            double y
        )
        {
            await OnRemove.InvokeAsync((movedItemId, targetItemId, fromId, toId, x, y));
        }

        [JSInvokable]
        public async Task OnUpdateJS(string movedItemId, string? targetItemId, string fromId)
        {
            await OnUpdate.InvokeAsync((movedItemId, targetItemId, fromId));
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                selfReference = DotNetObjectReference.Create(this);
                var module = await JS.InvokeAsync<IJSObjectReference>(
                    "import",
                    "../Components/Elements/Sortable/SortableList.razor.js"
                );

                await module.InvokeAsync<string>(
                    "init",
                    Id,
                    Group,
                    Pull,
                    Put,
                    Sort,
                    Handle,
                    Filter,
                    selfReference,
                    ForceFallback,
                    Direction,
                    Animation
                );
            }
        }
    }
}
