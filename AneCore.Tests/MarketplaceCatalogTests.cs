using Coflnet.Ane;
using Coflnet.Ane.ReferenceListings;

namespace AneCore.Tests;

[TestFixture]
public class MarketplaceCatalogTests
{
    [TestCase("https://www.ebay.de/itm/Camera/123456789012?campid=track", Platform.Ebay, "123456789012")]
    [TestCase("https://www.ebay.com.be/itm/123456789012?campid=track", Platform.Ebay, "123456789012")]
    [TestCase("https://www.kleinanzeigen.de/s-anzeige/camera/123456789-242-1", Platform.Kleinanzeigen, "123456789")]
    [TestCase("https://www.marktplaats.nl/v/audio/cameras/m12345678-camera", Platform.Marktplaats, "m12345678")]
    [TestCase("https://www.willhaben.at/iad/kaufen-und-verkaufen/d/camera-123456", Platform.Willhaben, "kaufen-und-verkaufen/d/camera-123456")]
    [TestCase("https://www.vinted.fr/items/123456-camera?ref=feed", Platform.Vinted, "123456")]
    [TestCase("https://www.shpock.com/de-de/i/ABC123/camera", Platform.Shpock, "ABC123")]
    [TestCase("https://www.facebook.com/marketplace/item/123456/", Platform.Facebook, "123456")]
    [TestCase("https://www.leboncoin.fr/ad/photo_audio_video/123456", Platform.Lebecoin, "123456")]
    [TestCase("https://es.wallapop.com/item/camera-123456", Platform.Wallapop, "123456")]
    [TestCase("https://www.olx.pl/d/oferta/camera-ID1cCcE4.html?reason=feed", Platform.OLX, "pl:ID1cCcE4")]
    [TestCase("https://www.subito.it/fotografia/camera-roma-123456.htm", Platform.Subito, "123456")]
    [TestCase("https://www.milanuncios.com/camaras/camera-123456.htm", Platform.Milanuncios, "123456")]
    [TestCase("https://allegro.pl/oferta/camera-123456", Platform.Allegro, "123456")]
    [TestCase("https://allegrolokalnie.pl/oferta/camera-abc", Platform.AllegroLokalnie, "camera-abc")]
    [TestCase("https://www.2dehands.be/v/audio/cameras/m123456-camera", Platform.TwoDehands, "m123456")]
    [TestCase("https://www.ricardo.ch/de/a/camera-123456/", Platform.Ricardo, "123456")]
    [TestCase("https://www.tutti.ch/de/vi/zuerich/foto/camera/123456", Platform.Tutti, "123456")]
    [TestCase("https://www.anibis.ch/de/d/camera-123456", Platform.Anibis, "123456")]
    [TestCase("https://www.tradera.com/item/1234/123456/camera", Platform.Tradera, "123456")]
    [TestCase("https://www.blocket.se/annons/stockholm/camera/123456", Platform.Blocket, "123456")]
    [TestCase("https://www.finn.no/bap/forsale/ad.html?finnkode=123456&ref=feed", Platform.Finn, "123456")]
    [TestCase("https://www.finn.no/recommerce/forsale/item/123456", Platform.Finn, "123456")]
    [TestCase("https://www.tori.fi/recommerce/forsale/item/123456", Platform.Tori, "123456")]
    [TestCase("https://www.dba.dk/camera/id-123456/", Platform.DBA, "123456")]
    [TestCase("https://foto.bazos.cz/inzerat/123456/camera.php", Platform.Bazos, "cz:123456")]
    [TestCase("https://www.sbazar.cz/seller/detail/123456-camera", Platform.Sbazar, "123456")]
    [TestCase("https://www.adverts.ie/cameras/camera/123456", Platform.Adverts, "123456")]
    [TestCase("https://www.donedeal.ie/cameras-for-sale/camera/123456", Platform.DoneDeal, "123456")]
    [TestCase("https://fr.shopping.rakuten.com/offer/buy/123456/camera.html", Platform.Rakuten, "123456")]
    [TestCase("https://www.catawiki.com/en/l/123456-camera", Platform.Catawiki, "123456")]
    [TestCase("https://www.delcampe.net/en/collectibles/cameras/camera-123456.html", Platform.Delcampe, "123456")]
    [TestCase("https://www.depop.com/products/seller-camera/", Platform.Depop, "seller-camera")]
    [TestCase("https://www.gumtree.com/p/cameras/camera/123456", Platform.Gumtree, "123456")]
    [TestCase("https://www.quoka.de/anzeigen/elektronik/hifi-audio-tv-video-foto/foto-und-zubehoer/sonstiges/anzeige/camera/abc123.html", Platform.Quoka, "abc123")]
    [TestCase("https://www.markt.de/berlin/cameras/camera/a/abc123/", Platform.MarktDe, "abc123")]
    [TestCase("https://vendora.gr/items/abc123/camera.html", Platform.Vendora, "abc123")]
    [TestCase("https://www.jofogas.hu/budapest/Camera_123456.htm", Platform.Jofogas, "123456")]
    [TestCase("https://www.njuskalo.hr/fotoaparati/camera-oglas-123456", Platform.Njuskalo, "123456")]
    public void ResolvesDetailUrls(string url, Platform platform, string id)
    {
        Assert.That(MarketplaceCatalog.TryResolve(url, out var address), Is.True);
        Assert.Multiple(() => { Assert.That(address.Platform, Is.EqualTo(platform)); Assert.That(address.Id, Is.EqualTo(id)); });
    }

    [TestCase("https://evil-ebay.de/itm/123456")]
    [TestCase("https://ebay.de.evil.example/itm/123456")]
    [TestCase("https://user:pass@ebay.de/itm/123456")]
    [TestCase("https://ebay.de:1234/itm/123456")]
    [TestCase("file:///itm/123456")]
    [TestCase("https://www.ebay.de/sch/i.html?q=camera")]
    [TestCase("https://www.vinted.fr/catalog?search_text=camera")]
    [TestCase("https://www.facebook.com/marketplace/")]
    [TestCase("https://www.cardmarket.com/en/Pokemon/Products/Singles/Base-Set/Charizard")]
    [TestCase("https://www.finn.no/bap/forsale/ad.html?finnkode=evil")]
    public void RejectsSpoofedUrlsAndNonListingPages(string url) =>
        Assert.That(MarketplaceCatalog.TryResolve(url, out _), Is.False);

    [Test]
    public void RemovesTrackingButRetainsFinnListingIdentity()
    {
        MarketplaceCatalog.TryResolve("https://www.finn.no/bap/forsale/ad.html?finnkode=123456&ref=feed#section", out var address);
        Assert.That(address.Url, Is.EqualTo("https://www.finn.no/bap/forsale/ad.html?finnkode=123456"));
    }

    [Test]
    public void RegionalIdsCannotCollide()
    {
        MarketplaceCatalog.TryResolve("https://www.olx.pl/d/oferta/camera-IDabc.html", out var pl);
        MarketplaceCatalog.TryResolve("https://www.olx.ro/d/oferta/camera-IDabc.html", out var ro);
        Assert.That(pl.Id, Is.Not.EqualTo(ro.Id));
    }

    [Test]
    public void AmbiguousSitesDoNotInventCurrencyOrSellerCountry()
    {
        MarketplaceCatalog.TryResolve("https://www.ebay.com/itm/123456", out var ebay);
        MarketplaceCatalog.TryResolve("https://www.facebook.com/marketplace/item/123456", out var facebook);
        Assert.Multiple(() => { Assert.That(ebay.Currency, Is.EqualTo("USD")); Assert.That(ebay.Country, Is.Empty); Assert.That(facebook.Currency, Is.Empty); });
    }

    [Test]
    public void StoredUrlMustHaveMatchingPlatformAndNativeId()
    {
        Assert.That(MarketplaceCatalog.ResolveUrl(Platform.Vinted, "123456", "https://www.vinted.fr/items/123456-camera", "de-DE"), Is.EqualTo("https://www.vinted.fr/items/123456-camera"));
        Assert.That(MarketplaceCatalog.ResolveUrl(Platform.Vinted, "123456", "https://evil.example/phishing", "de-DE"), Is.EqualTo("https://www.vinted.de/items/123456"));
    }
    [TestCase("www.ebay.ca")]
    [TestCase("www.ebay.com.au")]
    public void PreviewPreservesLegacyEbayDomainsOutsideBrowserCatalog(string host) =>
        Assert.That(ReferenceListingUrlPolicy.DetectMarketplace(host), Is.EqualTo("Ebay"));

}
