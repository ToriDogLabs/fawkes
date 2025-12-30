import { MutationOptions, QueryClient, QueryOptions, Updater, useQueryClient } from "@tanstack/react-query";

type DataFromOptions<T extends QueryOptions<any, any, any, any>> = T extends QueryOptions<any, any, infer TData, any>
	? TData | undefined
	: never;

function getHelpers(queryClient: QueryClient) {
	return {
		getQueryOptionsData<TOpts extends QueryOptions<any, any, any, any>>(opts: TOpts): DataFromOptions<TOpts> | undefined {
			return queryClient.getQueryData(opts.queryKey);
		},
		setQueryOptionsData<TOpts extends QueryOptions<any, any, any, any>>(
			opts: TOpts,
			updater: Updater<DataFromOptions<TOpts>, DataFromOptions<TOpts>>
		) {
			queryClient.setQueryData(opts.queryKey, updater);
		},
		executeMutation<TVariables>(options: MutationOptions<any, any, TVariables, any>, variables: TVariables) {
			return queryClient.getMutationCache().build(queryClient, options).execute(variables);
		},
	};
}

export type ExtendedQueryClient = QueryClient & ReturnType<typeof getHelpers>;

export function useQueryClientExt(): ExtendedQueryClient {
	const queryClient = useQueryClient();
	return getExtendedQueryClient(queryClient);
}

function getExtendedQueryClient(queryClient: QueryClient): ExtendedQueryClient {
	const helpers = getHelpers(queryClient);
	return Object.assign(queryClient, helpers);
}
