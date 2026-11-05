import { nest } from '../../js/sync.js';

export function attach(dotnet) {
    const shared = nest(window);
    shared.connect(dotnet);
    return {
        apply: updates => shared.apply(updates, dotnet),
        dispose: () => shared.disconnect(dotnet),
    };
}
