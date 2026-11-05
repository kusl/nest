namespace Nest.Web.Listings;

internal interface INestSubscriber
{
    bool Deliver(NestUpdate update);
}
